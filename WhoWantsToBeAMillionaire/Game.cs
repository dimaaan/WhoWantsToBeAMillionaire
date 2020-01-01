using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

    static readonly ReplyKeyboardMarkup YesNoKeyboard = new ReplyKeyboardMarkup()
    {
        keyboard = new KeyboardButton[][] {
            new [] {
                new KeyboardButton { text = "Да" },
                new KeyboardButton { text = "Нет" },
            },
        },
        one_time_keyboard = true
    };

    static class Commands
    {
        public const string Start = "/start";
        public const string Help = "/help";
    }

    public Game(BotApiClient botApi, Question[][] questions, Narrator narrator, ILogger<Game> logger, StateSerializer stateSerializer)
    {
        BotApi = botApi;
        Questions = questions;
        Narrator = narrator;
        Logger = logger;
        StateSerializer = stateSerializer;
        Games = new ConcurrentDictionary<long, States.State>(stateSerializer.Load());
    }

    public void Dispose()
    {
        StateSerializer.Save(Games);
    }

    public async Task UpdateGame(Update update, CancellationToken cancellationToken)
    {
        if (update.message != null)
        {
            Games.TryGetValue(update.message.chat.id, out var game);

            switch (game)
            {
                case null:
                    await OnStartState(update.message, cancellationToken);
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
    }

    async Task OnStartState(Message msg, CancellationToken cancellationToken)
    {
        switch (msg.text)
        {
            case Commands.Help:
                await Help(msg, cancellationToken);
                break;
            case Commands.Start:
            default:
                await StartGame(msg, Narrator.PickRandomGreetings(msg.from.first_name), cancellationToken);
                break;
        }
    }

    async Task OnPlayingState(Message msg, States.Playing state, CancellationToken cancellationToken)
    {
        var text = msg.text?.Trim()?.ToLowerInvariant();

        if(text == Commands.Start)
        {
            await ReplyTo(msg, "Вы уже в игре!", cancellationToken);
        }
        else if(text == Commands.Help)
        {
            await Help(msg, cancellationToken);
        }
        else if(text == Answers.FiftyFifty)
        {
            await FiftyFifty(msg, state, cancellationToken);
            return;
        }
        else if(text == Answers.CallFriend)
        {
            await CallFirend(msg, state, cancellationToken);
            return;
        }

        char? answer = text?.ToUpperInvariant() switch {
            Answers.A => 'A',
            Answers.B => 'B',
            Answers.C => 'C',
            Answers.D => 'D',
            _ => null
        };

        if(answer == null)
        {
            await ReplyTo(msg, "Отвечайте буквами A, B, C или D", cancellationToken);
            return;
        }

        var question = Questions[state.Level][state.Question];
        if (answer == question.RightAnswer)
        {
            var newLevel = (byte)(state.Level + 1);

            if (state.Level < 15)
            {
                await AskQuestion(msg, Narrator.PickRandomRightAnswerSpeech(newLevel, question), newLevel, state.UsedHints, cancellationToken);
            }
            else
            {
                await GameOver(msg, Narrator.PickRandomSpeechWin(), cancellationToken);
            }
        }
        else
        {
            await GameOver(msg, Narrator.PickRandomReplyToWrongAnswer(question), cancellationToken);
        }
    }

    async Task OnOverState(Message msg, CancellationToken cancellationToken)
    {
        switch(msg.text?.Trim().ToLower())
        {
            case "да":
            case Commands.Start:
                await StartGame(msg, "", cancellationToken);
                break;
            case "нет":
                Games.TryRemove(msg.chat.id, out _);
                break;
            case Commands.Help:
                await Help(msg, cancellationToken);
                break;
            default:
                await ReplyTo(msg, "Отвечайте \"да\" или \"нет\"", cancellationToken, YesNoKeyboard);
                break;
        }
    }

    async Task Help(Message msg, CancellationToken cancellationToken)
    {
        await ReplyTo(msg, "/start начать игру\n/help список команд", cancellationToken);
    }

    async Task StartGame(Message msg, string greetings, CancellationToken cancellationToken)
    {
        await AskQuestion(msg, greetings, 0, default, cancellationToken);
    }

    async Task AskQuestion(Message msg, string preamble, byte level, States.Playing.Hints usedHints, CancellationToken cancellationToken)
    {
        var questionIndex = Narrator.PickRandomIndex(Questions[level]);
        var question = Questions[level][questionIndex];
        var questionText = Narrator.PickRandomAskQuestionSpeech(msg.from.first_name, level, question);
        var text = $"{preamble}\n{questionText}";
        var state = new States.Playing()
        {
            Level = level,
            Question = questionIndex,
            UsedHints = usedHints
        };

        await ReplyTo(msg, text, cancellationToken, AnswersKeyboard(usedHints));

        Games[msg.chat.id] = state;
    }

    async Task FiftyFifty(Message msg, States.Playing state, CancellationToken cancellationToken)
    {
        if (!state.IsFiftyFiftyAvailable)
        {
            await ReplyTo(msg, "Вы уже использовали подсказку 50/50!", cancellationToken);
            return;
        }

        var question = Questions[state.Level][state.Question];
        var (text, removed1, removed2) = Narrator.FiftyFifty(question);
        var usedHints = state.UsedHints | States.Playing.Hints.FiftyFifty;

        await ReplyTo(msg, text, cancellationToken, Keyboard());

        state.UsedHints = usedHints;
        state.Removed1 = removed1;
        state.Removed2 = removed2;

        ReplyKeyboardMarkup Keyboard()
        {
            return new ReplyKeyboardMarkup
            {
                keyboard = Buttons(),
                one_time_keyboard = false,
            };

            IEnumerable<IEnumerable<KeyboardButton>> Buttons()
            {
                yield return AnswerButtonsRow('A', 'B');
                yield return AnswerButtonsRow('C', 'D');
                yield return HintButtonsRow(usedHints);

                IEnumerable<KeyboardButton> AnswerButtonsRow(char left, char right)
                {
                    if (NotRemoved(left, removed1, removed2, out var leftButton))
                        yield return leftButton!;

                    if (NotRemoved(right, removed1, removed2, out var rigthButton))
                        yield return rigthButton!;

                    static bool NotRemoved(char variant, char removed1, char removed2, out KeyboardButton? button)
                    {
                        var notRemoved = variant != removed1 && variant != removed2;
                        button = notRemoved ? new KeyboardButton { text = variant.ToString() } : null;
                        return notRemoved;
                    }
                }
            }
        }
    }

    async Task CallFirend(Message msg, States.Playing state, CancellationToken cancellationToken)
    {
        if(!state.IsCallFriendAvailable)
        {
            await ReplyTo(msg, "Вы уже звонили другу!", cancellationToken);
            return;
        }

        var question = Questions[state.Level][state.Question];
        var text = Narrator.CallFriend(msg.from.first_name, state.Level, question);
        var usedHints = state.UsedHints | States.Playing.Hints.CallFriend;

        await ReplyTo(msg, text, cancellationToken, AnswersKeyboard(usedHints));

        state.UsedHints = usedHints;
    }

    ReplyKeyboardMarkup AnswersKeyboard(States.Playing.Hints usedHints)
    {
        return new ReplyKeyboardMarkup()
        {
            keyboard = new KeyboardButton[][]
            {
                new []
                {
                    new KeyboardButton { text = Answers.A },
                    new KeyboardButton { text = Answers.B },
                },
                new []
                {
                    new KeyboardButton { text = Answers.C },
                    new KeyboardButton { text = Answers.D },
                },
                HintButtonsRow(usedHints).ToArray()
            },
            one_time_keyboard = false
        };
    }

    IEnumerable<KeyboardButton> HintButtonsRow(States.Playing.Hints usedHints)
    {
        if (!usedHints.HasFlag(States.Playing.Hints.FiftyFifty))
            yield return new KeyboardButton { text = Answers.FiftyFifty };

        if(!usedHints.HasFlag(States.Playing.Hints.CallFriend))
            yield return new KeyboardButton { text = Answers.CallFriend };
    }

    async Task GameOver(Message msg, string preamble, CancellationToken cancellationToken)
    {
        Games[msg.chat.id] = new States.Over();
        var text = $"{preamble}\n{Narrator.PickRandomTryAgainSpeech()}";
        await ReplyTo(msg, text, cancellationToken, YesNoKeyboard);
    }

    async Task ReplyTo(Message msg, string text, CancellationToken cancellationToken, ReplyKeyboardMarkup? markup = null) =>
        await Send(new SendMessageParams
        {
            chat_id = msg.chat.id,
            text = text,
            reply_markup = markup
        }, cancellationToken);

    async Task Send(SendMessageParams payload, CancellationToken cancellationToken) =>
        await BotApi.SendMessageAsync(payload, cancellationToken);
}

namespace States
{
    abstract class State
    {
    }

    class Playing : State
    {
        public byte Level;
        public short Question;
        public Hints UsedHints;
        public char Removed1;
        public char Removed2;

        public bool IsFiftyFiftyAvailable => !UsedHints.HasFlag(Hints.FiftyFifty);
        public bool IsCallFriendAvailable => !UsedHints.HasFlag(Hints.CallFriend);

        [Flags] public enum Hints : byte { FiftyFifty = 1, PeopleHelp = 2, CallFriend = 4 }
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
    public char RightAnswer { get; set; }

    public string RightAnswerText => AnswerOf(RightAnswer);

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
}