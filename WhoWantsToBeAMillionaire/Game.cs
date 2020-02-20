using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

class Game : IDisposable
{
    readonly BotApiClient BotApi;
    readonly Question[][] Questions;
    readonly Narrator Narrator;
    readonly ILogger<Game> Logger;
    readonly ConcurrentDictionary<long, States.State> Games;
    readonly StateSerializer StateSerializer;
    readonly EventLogger EventLogger;

    static class YesNoAnswers
    {
        public const string Yes = "да";
        public const string No = "нет";
    }

    static readonly ReplyKeyboardMarkup YesNoKeyboard = new ReplyKeyboardMarkup()
    {
        keyboard = new KeyboardButton[][] {
            new [] {
                new KeyboardButton { text = YesNoAnswers.Yes },
                new KeyboardButton { text = YesNoAnswers.No },
            },
        },
        one_time_keyboard = false
    };

    static class Commands
    {
        public const string Start = "/start";
        public const string Help = "/help";
    }

    public Game(BotApiClient botApi, Question[][] questions, Narrator narrator, ILogger<Game> logger, StateSerializer stateSerializer, EventLogger eventLogger)
    {
        BotApi = botApi;
        Questions = questions;
        Narrator = narrator;
        Logger = logger;
        StateSerializer = stateSerializer;
        Games = new ConcurrentDictionary<long, States.State>(stateSerializer.Load());
        EventLogger = eventLogger;
    }

    public void Dispose()
    {
        StateSerializer.Save(Games);
    }

    public int GamesCount => Games.Count;

    long _lastUpdatedAt;
    public DateTimeOffset LastUpdatedAt
    {
        get => DateTimeOffset.FromUnixTimeSeconds(Interlocked.Read(ref _lastUpdatedAt));
        private set => Interlocked.Exchange(ref _lastUpdatedAt, value.ToUnixTimeSeconds());
    }

    public async Task UpdateGame(Update update, CancellationToken cancellationToken)
    {
        if (update.message != null)
        {
            if (update.message.text?.Trim().ToLower() == Commands.Help)
            {
                await ReplyTo(update.message, Narrator.Help(), cancellationToken, markdown: true);
                return;
            }

            Games.TryGetValue(update.message.chat.id, out var game);

            switch (game)
            {
                case null:
                    await OnStartState(update.message, cancellationToken);
                    break;
                case States.WaitingTwoAnswers twoAnswers:
                    await OnWaitingTwoAnswersState(update.message, twoAnswers, cancellationToken);
                    break;
                case States.Playing playing:
                    await OnPlayingState(update.message, playing, cancellationToken);
                    break;
                case States.Over _:
                    await OnOverState(update.message, cancellationToken);
                    break;
            }
        }
        else
        {
            Logger.LogWarning("Update message wasn't handled");
        }

        LastUpdatedAt = DateTimeOffset.UtcNow;
    }

    async Task OnStartState(Message msg, CancellationToken cancellationToken)
    {
        if(msg.text?.Trim() == YesNoAnswers.No)
        {
            await ReplyTo(msg, "Ну нет, так нет.", cancellationToken);
            return;
        }

        await StartGame(msg, Narrator.Greetings(msg.from.first_name), cancellationToken);
    }

    async Task OnPlayingState(Message msg, States.Playing state, CancellationToken cancellationToken)
    {
        var text = msg.text?.Trim()?.ToLowerInvariant();

        if (text == Commands.Start)
        {
            await ReplyTo(msg, "Вы уже в игре!", cancellationToken);
        }
        else if (text == Answers.FiftyFifty)
        {
            await FiftyFifty(msg, state, cancellationToken);
            return;
        }
        else if (text == Answers.CallFriend)
        {
            await CallFirend(msg, state, cancellationToken);
            return;
        }
        else if(text == Answers.PeopleHelp)
        {
            await PeopleHelp(msg, state, cancellationToken);
            return;
        }
        else if (text == Answers.TwoAnswers)
        {
            await TwoAnswersBegin(msg, state, cancellationToken);
            return;
        }
        else if(text == Answers.NwQuestion)
        {
            await NewQuestion(msg, state, cancellationToken);
            return;
        }

        var answer = await ParseVariant(msg, cancellationToken);
        if (answer == default)
            return;

        await CheckAnswer(msg, state, answer, default, cancellationToken);
    }
    async Task CheckAnswer(Message msg, States.Playing state, char answer1, char answer2, CancellationToken cancellationToken)
    {
        var question = Questions[state.Level][state.Question];
        var isRightAnswer = answer1 == question.RightVariant || answer2 == question.RightVariant;

        if (isRightAnswer)
        {
            if (state.Level < 14)
            {
                var newLevel = (byte)(state.Level + 1);
                await AskQuestion(msg, Narrator.RightAnswerSpeech(newLevel, question), newLevel, state.UsedHints, cancellationToken);
            }
            else
            {
                await GameOver(msg, Narrator.WinSpeech(), cancellationToken);
            }
        }
        else
        {
            await GameOver(msg, Narrator.ReplyToWrongAnswer(state.Level, question), cancellationToken);
        }

        EventLogger.Answer(msg, state.Level, state.Question, answer1, answer2, isRightAnswer, cancellationToken);
    }

    async Task<char> ParseVariant(Message msg, CancellationToken cancellationToken)
    {
        var answer = msg.text?.Trim()?.ToUpperInvariant() switch
        {
            Answers.A => 'A',
            Answers.B => 'B',
            Answers.C => 'C',
            Answers.D => 'D',
            _ => default
        };

        if (answer == default)
            await ReplyTo(msg, "Отвечайте буквами A, B, C или D", cancellationToken);

        return answer;
    }

    async Task OnWaitingTwoAnswersState(Message msg, States.WaitingTwoAnswers state, CancellationToken cancellationToken)
    {
        var answer = await ParseVariant(msg, cancellationToken);
        if (answer == default)
            return;

        if (state.FirstAnswer == default)
            await TwoAnswersSetFirstAnswer(msg, answer, state, cancellationToken);
        else
            await TwoAnswersFinish(msg, answer, state, cancellationToken);
    }

    async Task OnOverState(Message msg, CancellationToken cancellationToken)
    {
        switch (msg.text?.Trim().ToLower())
        {
            case "да":
            case Commands.Start:
                await StartGame(msg, "", cancellationToken);
                break;
            case "нет":
                Games.TryRemove(msg.chat.id, out _);
                break;
            default:
                await ReplyTo(msg, "Отвечайте \"да\" или \"нет\"", cancellationToken, YesNoKeyboard);
                break;
        }
    }

    async Task StartGame(Message msg, string greetings, CancellationToken cancellationToken)
    {
        await AskQuestion(msg, greetings, 0, default, cancellationToken);
        EventLogger.StartGame(msg, cancellationToken);
    }

    async Task AskQuestion(Message msg, string preamble, byte level, States.Playing.Hints usedHints, CancellationToken cancellationToken)
    {
        var questionIndex = Narrator.PickRandomIndex(Questions[level]);
        var question = Questions[level][questionIndex];
        var questionText = Narrator.AskQuestionSpeech(msg.from.first_name, level, question);
        var text = $"{preamble}\n{questionText}";
        var newState = new States.Playing(level, questionIndex, usedHints);

        await ReplyTo(msg, text, cancellationToken, AnswersKeyboard(newState));

        Games[msg.chat.id] = newState;
    }

    async Task FiftyFifty(Message msg, States.Playing state, CancellationToken cancellationToken)
    {
        if (state.UsedHints.HasFlag(States.Playing.Hints.FiftyFifty))
        {
            await ReplyTo(msg, "Вы уже использовали подсказку 50/50!", cancellationToken);
            return;
        }

        var question = Questions[state.Level][state.Question];
        var (text, removed1, removed2) = Narrator.FiftyFifty(question);
        var newState = new States.Playing(state.Level, state.Question, state.UsedHints | States.Playing.Hints.FiftyFifty, removed1, removed2);

        await ReplyTo(msg, text, cancellationToken, AnswersKeyboard(newState));
        EventLogger.Hint(msg, state.Level, state.Question, "50", cancellationToken);

        Games[msg.chat.id] = newState;
    }

    async Task CallFirend(Message msg, States.Playing state, CancellationToken cancellationToken)
    {
        if (state.UsedHints.HasFlag(States.Playing.Hints.CallFriend))
        {
            await ReplyTo(msg, "Вы уже звонили другу!", cancellationToken);
            return;
        }

        var question = Questions[state.Level][state.Question];
        var text = Narrator.CallFriend(msg.from.first_name, state.Level, question, state.Removed1, state.Removed2);
        var newState = new States.Playing(state.Level, state.Question, state.UsedHints | States.Playing.Hints.CallFriend, state.Removed1, state.Removed2);

        await ReplyTo(msg, text, cancellationToken, AnswersKeyboard(newState));

        EventLogger.Hint(msg, state.Level, state.Question, "cf", cancellationToken);

        Games[msg.chat.id] = newState;
    }

    async Task PeopleHelp(Message msg, States.Playing state, CancellationToken cancellationToken)
    {
        if (state.UsedHints.HasFlag(States.Playing.Hints.PeopleHelp))
        {
            await ReplyTo(msg, "Вы уже просили зал о помощи!", cancellationToken);
            return;
        }

        var question = Questions[state.Level][state.Question];
        var text = Narrator.PeopleHelp(msg.from.first_name, state.Level, question, state.Removed1, state.Removed2);
        var newState = new States.Playing(state.Level, state.Question, state.UsedHints | States.Playing.Hints.PeopleHelp, state.Removed1, state.Removed2);

        await ReplyTo(msg, text, cancellationToken, AnswersKeyboard(newState));

        EventLogger.Hint(msg, state.Level, state.Question, "ph", cancellationToken);

        Games[msg.chat.id] = newState;
    }

    async Task TwoAnswersBegin(Message msg, States.Playing state, CancellationToken cancellationToken)
    {
        if (state.UsedHints.HasFlag(States.Playing.Hints.TwoAnswers))
        {
            await ReplyTo(msg, "Вы уже брали эту подсказку!", cancellationToken);
            return;
        }

        var text = Narrator.TwoAnswersStep1();
        var newState = new States.WaitingTwoAnswers(state);

        await ReplyTo(msg, text, cancellationToken, AnswersKeyboard(newState));
        EventLogger.Hint(msg, state.Level, state.Question, "2a", cancellationToken);

        Games[msg.chat.id] = newState;
    }

    async Task TwoAnswersSetFirstAnswer(Message msg, char firstAnswer, States.WaitingTwoAnswers state, CancellationToken cancellationToken)
    {
        var text = Narrator.TwoAnswersStep2();
        var newState = new States.WaitingTwoAnswers(state, firstAnswer);

        await ReplyTo(msg, text, cancellationToken, AnswersKeyboard(newState));

        Games[msg.chat.id] = newState;
    }

    async Task TwoAnswersFinish(Message msg, char secondAnswer, States.WaitingTwoAnswers state, CancellationToken cancellationToken)
    {
        if(secondAnswer == state.FirstAnswer)
        {
            await ReplyTo(msg, $"Вы уже выбрали {secondAnswer}. Выберите что-нибудь другое.", cancellationToken, AnswersKeyboard(state));
            return;
        }

        await CheckAnswer(msg, state, state.FirstAnswer, secondAnswer, cancellationToken);
    }

    async Task NewQuestion(Message msg, States.Playing state, CancellationToken cancellationToken)
    {
        if (state.UsedHints.HasFlag(States.Playing.Hints.NwQuestion))
        {
            await ReplyTo(msg, "Вы уже меняли вопрос!", cancellationToken);
            return;
        }

        short questionIndex;
        do
        {
            questionIndex = Narrator.PickRandomIndex(Questions[state.Level]);
        } while (questionIndex == state.Question);

        var question = Questions[state.Level][questionIndex];
        var questionText = Narrator.FormatQuestion(question);
        var text = $"{Narrator.NewQuestion()}\n{questionText}";
        var newState = new States.Playing(state.Level, questionIndex, state.UsedHints | States.Playing.Hints.NwQuestion);

        await ReplyTo(msg, text, cancellationToken, AnswersKeyboard(newState));
        EventLogger.Hint(msg, state.Level, state.Question, "nq", cancellationToken);

        Games[msg.chat.id] = newState;
    }

    ReplyKeyboardMarkup AnswersKeyboard(States.Playing state)
    {
        return new ReplyKeyboardMarkup
        {
            keyboard = Buttons(state),
            one_time_keyboard = false,
        };

        static IEnumerable<IEnumerable<KeyboardButton>> Buttons(States.Playing state)
        {
            yield return AnswerButtonsRow();

            if (state is States.WaitingTwoAnswers)
                yield break;

            yield return HintButtonsRow1(state.UsedHints);
            yield return HintButtonsRow2(state.UsedHints);

            IEnumerable<KeyboardButton> AnswerButtonsRow()
            {
                if (!Removed('A', state, out var a))
                    yield return a!;

                if (!Removed('B', state, out var b))
                    yield return b!;

                if (!Removed('C', state, out var c))
                    yield return c!;

                if (!Removed('D', state, out var d))
                    yield return d!;

                static bool Removed(char variant, States.Playing state, out KeyboardButton? button)
                {
                    var removed = variant == state.Removed1
                        || variant == state.Removed2
                        || (state is States.WaitingTwoAnswers w ? variant == w.FirstAnswer : false);

                    button = !removed ? new KeyboardButton { text = variant.ToString() } : null;
                    return removed;
                }
            }

            static IEnumerable<KeyboardButton> HintButtonsRow1(States.Playing.Hints usedHints)
            {
                if (!usedHints.HasFlag(States.Playing.Hints.FiftyFifty))
                    yield return new KeyboardButton { text = Answers.FiftyFifty };

                if (!usedHints.HasFlag(States.Playing.Hints.CallFriend))
                    yield return new KeyboardButton { text = Answers.CallFriend };

                if (!usedHints.HasFlag(States.Playing.Hints.PeopleHelp))
                    yield return new KeyboardButton { text = Answers.PeopleHelp };
            }

            static IEnumerable<KeyboardButton> HintButtonsRow2(States.Playing.Hints usedHints)
            {
                if (!usedHints.HasFlag(States.Playing.Hints.TwoAnswers))
                    yield return new KeyboardButton { text = Answers.TwoAnswers };

                if (!usedHints.HasFlag(States.Playing.Hints.NwQuestion))
                    yield return new KeyboardButton { text = Answers.NwQuestion };
            }
        }
    }

    async Task GameOver(Message msg, string preamble, CancellationToken cancellationToken)
    {
        Games[msg.chat.id] = new States.Over();
        var text = $"{preamble}\n{Narrator.TryAgainSpeech()}";
        await ReplyTo(msg, text, cancellationToken, YesNoKeyboard);
    }

    async Task ReplyTo(Message msg, string text, CancellationToken cancellationToken, ReplyKeyboardMarkup? markup = null, bool markdown = false)
    {
        var payload = new SendMessageParams
        {
            chat_id = msg.chat.id,
            text = text,
            parse_mode = markdown ? "Markdown" : null,
            disable_notification = true,
            reply_markup = markup
        };

        try
        {
            await BotApi.SendMessageAsync(payload, cancellationToken);
        }
        catch(BotApiException e)
        {
            Logger.LogWarning(e, "Error replying to {UserText} in chat {ChatId} with text {Text}", msg.text, payload.chat_id, payload.text);
        }
    }
}

namespace States
{
    abstract class State
    {
    }

    class Playing : State
    {
        public Playing(byte level, short question, Hints usedHints)
            : this(level, question, usedHints, default, default)
        { }

        public Playing(byte level, short question, Hints usedHints, char removed1, char removed2) =>
            (Level, Question, UsedHints, Removed1, Removed2) = (level, question, usedHints, removed1, removed2);

        public readonly byte Level;
        public readonly short Question;
        public readonly Hints UsedHints;
        public readonly char Removed1;
        public readonly char Removed2;

        [Flags] public enum Hints : byte { 
            FiftyFifty = 0b_0000_0001,
            PeopleHelp = 0b_0000_0010,
            CallFriend = 0b_0000_0100,
            CanMistake = 0b_0000_1000,
            TwoAnswers = 0b_0001_0000,
            NwQuestion = 0b_0010_0000,
        }
    }

    class WaitingTwoAnswers : Playing
    {
        public WaitingTwoAnswers(Playing p)
            : base(p.Level, p.Question, p.UsedHints | Hints.TwoAnswers, p.Removed1, p.Removed2)
        { }

        public WaitingTwoAnswers(Playing p, char firstAnswer)
            : this(p) =>
            (FirstAnswer) = (firstAnswer);

        public readonly char FirstAnswer;
    }

    class Over : State
    {
    }
}

class Question
{
    public string Text { get; set; } = default!;
    public string A { get; set; } = default!;
    public string B { get; set; } = default!;
    public string C { get; set; } = default!;
    public string D { get; set; } = default!;
    public char RightVariant { get; set; }

    public string RightAnswer => AnswerOf(RightVariant);

    public string AnswerOf(char variant) => variant switch
    {
        'A' => A,
        'B' => B,
        'C' => C,
        'D' => D,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Expected values: A, B, C, D")
    };
}

static class Answers
{
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
    public const string FiftyFifty = "50/50";
    public const string CallFriend = "звонок другу";
    public const string PeopleHelp = "помощь зала";
    public const string TwoAnswers = "право на ошибку";
    public const string NwQuestion = "замена вопроса";
}