using System;

namespace States
{
    public abstract class State
    {
    }

    public class Playing : State
    {
        public Playing(byte level, short question, Hints usedHints)
            : this(level, question, usedHints, null, null)
        { }

        public Playing(byte level, short question, Hints usedHints, char? removed1, char? removed2) =>
            (Level, Question, UsedHints, Removed1, Removed2) = (level, question, usedHints, removed1, removed2);

        public readonly byte Level;
        public readonly short Question;
        public readonly Hints UsedHints;
        public readonly char? Removed1;
        public readonly char? Removed2;

        [Flags]
        public enum Hints : byte
        {
            FiftyFifty = 0b_0000_0001,
            PeopleHelp = 0b_0000_0010,
            CallFriend = 0b_0000_0100,
            TwoAnswers = 0b_0000_1000,
            NwQuestion = 0b_0001_0000,
        }
    }

    public class WaitingTwoAnswers : Playing
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