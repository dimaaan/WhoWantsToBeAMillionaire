/// <summary>
/// Texts, that Narrator sends to the player. 
/// Each field is an array of texts for different game state or situation. 
/// Each array item is a slightly different verbiage. 
/// Narrator should pick them randomly to avoid repeating, acting more like a human. 
/// Some text contains placeholders that must be replaced with actual info.
/// </summary>
public class Speech
{
    /// <summary>
    /// 0 - user name
    /// </summary>
    public string[] StartGame { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - question
    /// 1 - user name
    /// </summary>
    public string[] FirstQuestion { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - question
    /// 1 - user name
    /// 2 - question no
    /// 3 - question sum
    /// 4 - earned money
    /// </summary>
    public string[] AskQuestion { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - variant char,
    /// 1 - variant text
    /// 2 - question sum
    /// </summary>
    public string[] RightAnswer { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - variant char,
    /// 1 - variant text
    /// 2 - question sum
    /// </summary>
    public string[] EarnedCantFire { get; set; } = default!;

    /// <summary>
    /// Placeholders:
    /// 0 - right variant,
    /// 1 - right answer
    /// </summary>
    public string[] WrongAnswer { get; set; } = default!;

    public string[] Win { get; set; } = default!;

    public string[] RequestLimit { get; set; } = default!;

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

    public string[] TwoAnswersStep1 { get; set; } = default!;

    public string[] TwoAnswersStep2 { get; set; } = default!;

    public string[] NewQuestion { get; set; } = default!;

    public string[] TryAgain { get; set; } = default!;
}