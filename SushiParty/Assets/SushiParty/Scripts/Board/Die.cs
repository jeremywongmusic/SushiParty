namespace SushiParty.Board
{
    public interface IDie
    {
        int Roll();

        int Faces { get; }
    }

    public sealed class RandomDie : IDie
    {
        private readonly System.Random random;

        public RandomDie()
            : this(System.Environment.TickCount)
        {
        }

        public RandomDie(int seed)
        {
            random = new System.Random(seed);
        }

        public int Faces => 6;

        public int Roll()
        {
            return random.Next(1, Faces + 1);
        }
    }

    public sealed class ScriptedDie : IDie
    {
        private readonly int[] values;
        private int next;

        public ScriptedDie(params int[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new System.ArgumentException("a scripted die needs at least one value", nameof(values));
            }

            this.values = values;
        }

        public int Faces => 6;
        public int RollCount => next;

        public int Roll()
        {
            int value = values[next < values.Length ? next : values.Length - 1];
            next++;
            return value;
        }
    }
}
