using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

class GameService : IDisposable
{
    readonly BotApiClient BotApi;
    readonly Strings Strings;
    readonly ILogger<GameService> Logger;
    readonly ConcurrentDictionary<long, States.State> Games;
    readonly StateSerializerService StateSerializer;

    static readonly Random Rnd = new Random();
    static readonly int[] ScoreTable = {
        0,
        100,
        200,
        300,
        500,
        1000,
        2000,
        4000,
        8000,
        16000,
        32000,
        64000,
        125000,
        250000,
        500000,
        1000000
    };
    static readonly ReplyKeyboardMarkup AnswerKeyboard = new ReplyKeyboardMarkup()
    {
        keyboard = new KeyboardButton[][] { 
            new [] { 
                new KeyboardButton { text = "A" },
                new KeyboardButton { text = "B" },
            }, 
            new [] {
                new KeyboardButton { text = "C" },
                new KeyboardButton { text = "D" },
            } 
        },
        one_time_keyboard = false
    };

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

    public GameService(BotApiClient botApi, Strings strings, ILogger<GameService> logger, StateSerializerService stateSerializer)
    {
        BotApi = botApi;
        Strings = strings;
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
                    await OnPlayingState(playing, update.message, cancellationToken);
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
            case "/help":
                await ReplyTo(msg, "/start начать игру\n/help список команд", cancellationToken);
                break;
            case "/start":
            default:
                await StartGame(msg, cancellationToken);
                break;
        }
    }

    async Task OnPlayingState(States.Playing state, Message msg, CancellationToken cancellationToken)
    {
        char? answer = msg.text?.Trim().ToUpper() switch {
            "A" => 'A',
            "B" => 'B',
            "C" => 'C',
            "D" => 'D',
            _ => null
        };

        if(answer == null)
        {
            await ReplyTo(msg, "Отвечайте буквами A, B, C или D", cancellationToken);
            return;
        }

        var question = Strings.Questions[state.Level][state.Question];
        if (answer == question.RightAnswer)
        {
            var newLevel = (byte)(state.Level + 1);
            await ReplyTo(msg, PickRandomRightAnswerSpeech(newLevel, question), cancellationToken);

            if (state.Level < 15)
            {
                await AskQuestion(msg, newLevel, cancellationToken);
            }
            else
            {
                await ReplyTo(msg, PickRandomSpeechWin(), cancellationToken);
                await GameOver(msg, cancellationToken);
            }
        }
        else
        {
            await ReplyTo(msg, PickRandomReplyToWrongAnswer(question), cancellationToken);
            await GameOver(msg, cancellationToken);
        }
    }

    async Task OnOverState(Message msg, CancellationToken cancellationToken)
    {
        switch(msg.text?.Trim().ToUpper())
        {
            case "ДА":
                await AskQuestion(msg, 0, cancellationToken);
                break;
            case "НЕТ":
                Games.TryRemove(msg.chat.id, out _);
                break;
            default:
                await ReplyTo(msg, "Отвечайте \"да\" или \"нет\"", cancellationToken, YesNoKeyboard);
                break;
        }
    }

    async Task StartGame(Message msg, CancellationToken cancellationToken)
    {
        await ReplyTo(msg, PickRandomGreetings(msg.from.first_name), cancellationToken);
        await AskQuestion(msg, 0, cancellationToken);
    }

    async Task AskQuestion(Message msg, byte level, CancellationToken cancellationToken)
    {
        var questionIndex = PickRandomIndex(Strings.Questions[level]);
        var question = Strings.Questions[level][questionIndex];

        await ReplyTo(msg, PickRandomAskQuestionSpeech(msg.from.first_name, level, question), cancellationToken, AnswerKeyboard);

        Games[msg.chat.id] = new States.Playing()
        {
            Level = level,
            Question = questionIndex
        };
    }

    async Task GameOver(Message msg, CancellationToken cancellationToken)
    {
        Games[msg.chat.id] = new States.Over();
        await ReplyTo(msg, PickRandomTryAgainSpeech(), cancellationToken, YesNoKeyboard);
    }

    async Task ReplyTo(Message msg, string text, CancellationToken cancellationToken, ReplyKeyboardMarkup? markup = null) =>
        await Send(new SendMessageParams
        {
            chat_id = msg.chat.id,
            text = text,
            reply_markup = markup
        }, cancellationToken);

    async Task Send(SendMessageParams payload, CancellationToken cancellationToken) =>
        await BotApi.SendMessage(payload, cancellationToken);

    string PickRandomSpeechWin() =>
        PickRandomItem(Strings.Speech.Win);

    string PickRandomRightAnswerSpeech(byte level, Question question)
    {
        var template = PickRandomItem(Strings.Speech.RightAnswer);

        if(level == 5 || level == 10)
            template = $"{template}\n{PickRandomItem(Strings.Speech.EarnedCantFire)}";

        return String.Format(
            template,
            question.RightAnswer,
            question.RightAnswerText,
            ScoreTable[level]
        );
    }

    string PickRandomTryAgainSpeech() =>
        PickRandomItem(Strings.Speech.TryAgain);

    string PickRandomReplyToWrongAnswer(Question question) =>
        String.Format(
            PickRandomItem(Strings.Speech.WrongAnswer), 
            question.RightAnswer, 
            question.RightAnswerText
        );

    string PickRandomAskQuestionSpeech(string userName, byte level, Question question) =>
        String.Format(
            PickRandomItem(Strings.Speech.AskQuestion),
            userName,
            question.Text,
            question.A,
            question.B,
            question.C,
            question.D,
            level + 1,
            ScoreTable[level + 1],
            ScoreTable[level]
        );

    string PickRandomGreetings(string name) =>
        String.Format(PickRandomItem(Strings.Speech.StartGame), name);

    static TItem PickRandomItem<TItem>(TItem[] array) =>
        array[PickRandomIndex(array)];

    static int PickRandomIndex<TItem>(TItem[] c) =>
        Rnd.Next(0, c.Length);
}

namespace States
{
    abstract class State
    {
    }

    class Playing : State
    {
        public byte Level;
        public int Question;
    }

    class Over : State
    {
    }
}

class Strings
{
    public Question[][] Questions { get; set; } = default!;
    public Speech Speech { get; set; } = default!;
}

class Question
{
    public string Text { get; set; } = default!;
    public string A { get; set; } = default!;
    public string B { get; set; } = default!;
    public string C { get; set; } = default!;
    public string D { get; set; } = default!;
    public char RightAnswer { get; set; }
    public string RightAnswerText
    {
        get => RightAnswer switch
        {
            'A' => A,
            'B' => B,
            'C' => C,
            'D' => D,
            _ => throw new InvalidOperationException($"{nameof(RightAnswer)} has invalid value: {RightAnswer}")
        };
    }
}

class Speech
{
    /// <summary>
    /// 0 - user name
    /// </summary>
    public string[] StartGame { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - user name
    /// 1 - question
    /// 2 - variant A
    /// 3 - variant B
    /// 4 - variant C
    /// 5 - variant D
    /// 6 - question no
    /// 7 - question sum
    /// 8 - earned money
    /// </summary>
    public string[] AskQuestion { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - variant char,
    /// 1 - variant text
    /// 2 - question sum
    /// 3 - earned money
    /// </summary>
    public string[] RightAnswer { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - variant char,
    /// 1 - variant text
    /// 2 - question sum
    /// 3 - earned money
    /// </summary>
    public string[] WrongAnswer { get; set; } = default!;

    public string[] Win { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - user name
    /// 1 - friend name
    /// 2 - question
    /// 3 - friend's variant char
    /// 4 - friend's variant text
    /// </summary>
    public string[][] CallFriend { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - user name
    /// 1 - question
    /// </summary>
    public string[] PeopleHelp { get; set; } = default!;

    public string[] FiftyFifty { get; set; } = default!;

    public string[] TryAgain { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - variant char,
    /// 1 - variant text
    /// 2 - question sum
    /// 3 - earned money
    /// </summary>
    public string[] EarnedCantFire { get; set; } = default!;
}