namespace Halibut.Tests.Support
{
    public class Reference<T>
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Reference()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public Reference(T value)
        {
            Value = value;
        }

        public T Value { get; set; }
    }
}
