using System;
using System.Collections.Generic;
using System.Text;

class Narrator
{
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

    readonly Speech Speech;

    public Narrator(Speech speech)
    {
        Speech = speech;
    }

    public string Help() =>
        @"Чтобы заработать миллион рублей, нужно ответить на 15 вопросов.
Каждый вопрос имеет 4 варианта ответа, из которых только один является верным.
Каждый правильный ответ повышает сумму выигрыша:
```
 вопр. │ выигрыш   
     1 │ 100₽      
     2 │ 200₽      
     3 │ 300₽      
     4 │ 500₽      
     5 │ 1000₽     
     6 │ 2000₽     
     7 │ 4000₽     
     8 │ 8000₽     
     9 │ 16 000₽   
    10 │ 32 000₽   
    11 │ 64 000₽   
    12 │ 125 000₽  
    13 │ 250 000₽  
    14 │ 500 000₽  
    15 │ 1 000 000₽
```
Cуммы, полученные при верном ответе на 5-й и 10-й вопросы, являлись «несгораемыми».
Они останутся у игрока даже при неправильном ответе на один из следующих вопросов.
В случае неверного ответа — игра заканчивается, а выигрыш — последняя «несгораемая» сумма.
Игроку предлагается 4 единоразовые подсказки:
• «Помощь зала» — зрители в студии голосуют за правильный, на их взгляд, ответ, и игрок видит статистику.
• «50/50» — ведущий убирает два неправильных варианта ответа.
• «Звонок другу» — игрок может посоветоваться с вируальным другом.
• «Замена вопроса» — заменить вопрос на другой, такой же сложности";

    public string Greetings(string userName) =>
        String.Format(PickRandomItem(Speech.StartGame), userName);

    public string AskQuestionSpeech(string userName, byte level, Question question) =>
        String.Format(
            PickRandomItem(Speech.AskQuestion),
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

    public string ReplyToWrongAnswer(byte level, Question question)
    {
        var text = PickRandomItem(Speech.WrongAnswer);

        if(level >= 5)
        {
            var earned = level >= 10 ? ScoreTable[10] : ScoreTable[5];
            text = $"{text}\nНо вы заработали {earned} рублей, поздравляю!";
        }

        return string.Format(text, question.RightVariant, question.RightAnswer);
    }

    public string RightAnswerSpeech(byte level, Question question)
    {
        var template = PickRandomItem(Speech.RightAnswer);

        if (level == 5 || level == 10)
            template = $"{template}\n{PickRandomItem(Speech.EarnedCantFire)}";

        return String.Format(
            template,
            question.RightVariant,
            question.RightAnswer,
            ScoreTable[level]
        );
    }

    public (string text, char removed1, char removed2) FiftyFifty(Question question)
    {
        var wrongsVariants = new List<char>(4) { 'A', 'B', 'C', 'D' };
        wrongsVariants.Remove(question.RightVariant);
        var removed1 = PickRandomItem(wrongsVariants);
        wrongsVariants.Remove(removed1);
        var removed2 = PickRandomItem(wrongsVariants);

        var text = $"{PickRandomItem(Speech.FiftyFifty)}\n{FormatQuestion(question, removed1, removed2)}";

        return (text, removed1, removed2);
    }

    public string FormatQuestion(Question question, char removed1 = default, char removed2 = default)
    {
        var text = new StringBuilder(question.Text);
        text.Append('\n');
        AppendIfNotRemoved('A', question.A);
        AppendIfNotRemoved('B', question.B);
        AppendIfNotRemoved('C', question.C);
        AppendIfNotRemoved('D', question.D);

        return text.ToString();

        void AppendIfNotRemoved(char variant, string answer)
        {
            if (variant != removed1 && variant != removed2)
                text.AppendFormat("\u2022 {0}: {1}\n", variant, answer);
        }
    }

    public string PeopleHelp(string userName, byte level, Question question, char removed1, char removed2)
    {
        var availableVariants = new List<char>(4) { 'A', 'B', 'C', 'D' };
        availableVariants.Remove(removed1);
        availableVariants.Remove(removed2);

        Span<(char Var, byte Percent)> tableRows = stackalloc (char, byte)[]
        {
            ('A', 0),
            ('B', 0),
            ('C', 0),
            ('D', 0),
        };

        for (var i = 0; i < 100; i++)
        {
            var row = GuessAnswer(availableVariants, question.RightVariant, level) switch
            {
                'A' => 0,
                'B' => 1,
                'C' => 2,
                _ => 3,
            };
            tableRows[row].Percent++;
        }

        var speech = String.Format(PickRandomItem(Speech.PeopleHelp), userName, question.Text);
        var table = new StringBuilder(speech);
        table.Append('\n');
        foreach (var (Var, Percent) in tableRows)
            table.AppendFormat("{0} |{1} {2}%\n", Var, new string('-', Percent / 5), Percent);

        return table.ToString();
    }

    public string CallFriend(string userName, byte level, Question question, char removed1, char removed2)
    {
        var availableVariants = new List<char> { 'A', 'B', 'C', 'D' };
        availableVariants.Remove(removed1);
        availableVariants.Remove(removed2);

        var template = String.Join('\n', PickRandomItem(Speech.CallFriend));
        var friendName = PickRandomItem(Speech.FriendsNames);
        var friendVariant = GuessAnswer(availableVariants, question.RightVariant, level);
        return String.Format(template, userName, friendName, question.Text, friendVariant, question.AnswerOf(friendVariant));
    }

    public string NewQuestion() =>
        PickRandomItem(Speech.NewQuestion);

    public string TryAgainSpeech() =>
        PickRandomItem(Speech.TryAgain);

    public string WinSpeech() =>
        PickRandomItem(Speech.Win);

    TItem PickRandomItem<TItem>(IList<TItem> items) =>
        items[PickRandomIndex(items)];

    public short PickRandomIndex<T>(ICollection<T> c) =>
        (short)Rnd.Next(0, c.Count);

    static readonly double[] ProbabilityOfRightHintTable = {
        .7,
        .7,
        .65,
        .6,
        .55,
        .5,
        .45,
        .4,
        .35,
        .3,
        .3,
        .25,
        .25,
        .15,
        .15,
        .1
    };

    char GuessAnswer(IList<char> availableVariants, char rightVariant, byte level) =>
        Rnd.NextDouble() <= ProbabilityOfRightHintTable[level]
            ? rightVariant
            : PickRandomItem(availableVariants);
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

    public string[] FriendsNames { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - user name
    /// 1 - question
    /// </summary>
    public string[] PeopleHelp { get; set; } = default!;

    public string[] FiftyFifty { get; set; } = default!;

    public string[] NewQuestion { get; set; } = default!;

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