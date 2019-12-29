using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
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
            case Commands.Help:
                await Help(msg, cancellationToken);
                break;
            case Commands.Start:
            default:
                await StartGame(msg, cancellationToken);
                break;
        }
    }

    async Task OnPlayingState(States.Playing state, Message msg, CancellationToken cancellationToken)
    {
        if(msg.text == Commands.Start)
        {
            await ReplyTo(msg, "Вы уже в игре!", cancellationToken);
        }
        else if(msg.text == Commands.Help)
        {
            await Help(msg, cancellationToken);
        }

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

        var question = Questions[state.Level][state.Question];
        if (answer == question.RightAnswer)
        {
            var newLevel = (byte)(state.Level + 1);

            if (state.Level < 15)
            {
                await AskQuestion(msg, Narrator.PickRandomRightAnswerSpeech(newLevel, question), newLevel, cancellationToken);
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
                await AskQuestion(msg, "", 0, cancellationToken);
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

    async Task StartGame(Message msg, CancellationToken cancellationToken)
    {
        await AskQuestion(msg, Narrator.PickRandomGreetings(msg.from.first_name), 0, cancellationToken);
    }

    async Task AskQuestion(Message msg, string preamble, byte level, CancellationToken cancellationToken)
    {
        var questionIndex = Narrator.PickRandomIndex(Questions[level]);
        var question = Questions[level][questionIndex];
        var questionText = Narrator.PickRandomAskQuestionSpeech(msg.from.first_name, level, question);
        var text = $"{preamble}\n{questionText}";

        await ReplyTo(msg, text, cancellationToken, AnswerKeyboard);

        Games[msg.chat.id] = new States.Playing()
        {
            Level = level,
            Question = questionIndex
        };
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