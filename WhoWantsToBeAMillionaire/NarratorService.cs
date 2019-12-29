using System;

class NarratorService
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

    public NarratorService(Speech speech)
    {
        Speech = speech;
    }

    public string PickRandomGreetings(string name) =>
        String.Format(PickRandomItem(Speech.StartGame), name);

    public string PickRandomAskQuestionSpeech(string userName, byte level, Question question) =>
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

    public string PickRandomReplyToWrongAnswer(Question question) =>
        String.Format(
            PickRandomItem(Speech.WrongAnswer),
            question.RightAnswer,
            question.RightAnswerText
        );

    public string PickRandomRightAnswerSpeech(byte level, Question question)
    {
        var template = PickRandomItem(Speech.RightAnswer);

        if (level == 5 || level == 10)
            template = $"{template}\n{PickRandomItem(Speech.EarnedCantFire)}";

        return String.Format(
            template,
            question.RightAnswer,
            question.RightAnswerText,
            ScoreTable[level]
        );
    }

    public string PickRandomTryAgainSpeech() =>
        PickRandomItem(Speech.TryAgain);

    public string PickRandomSpeechWin() =>
        PickRandomItem(Speech.Win);

    TItem PickRandomItem<TItem>(TItem[] array) =>
        array[PickRandomIndex(array)];

    public short PickRandomIndex<TItem>(TItem[] c) =>
        (short)Rnd.Next(0, c.Length);
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