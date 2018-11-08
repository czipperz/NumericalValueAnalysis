using System;
using System.Collections.Generic;
using System.Text;

namespace HelloRoslyn
{
    public class NumericalValue<T>
    {
        SortedSet<Range> ranges;

        public NumericalValue() {
            ranges = new SortedSet<Range>();
        }

        public NumericalValue(T min, Inclusivity minI, T max, Inclusivity maxI)
        {
            ranges = new SortedSet<Range>();
            ranges.Add(new Range(new Pair(min, minI), new Pair(max, maxI)));
        }

        private NumericalValue(SortedSet<Range> ranges)
        {
            this.ranges = new SortedSet<Range>();
            foreach (var r in ranges)
            {
                this.ranges.Add(r.Clone());
            }
        }

        public NumericalValue(T v)
        {
            ranges = new SortedSet<Range>();
            ranges.Add(new Range(new Pair(v, Inclusivity.INCLUSIVE), new Pair(v, Inclusivity.INCLUSIVE)));
        }

        public NumericalValue<T> Clone()
        {
            return new NumericalValue<T>(ranges);
        }

        public override string ToString()
        {
            var s = new StringBuilder();
            bool first = true;
            foreach (Range n in ranges)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    s.Append(" U ");
                }
                s.AppendFormat("{0}{1}, {2}{3}",
                    n.min.i == Inclusivity.EXCLUSIVE ? "(" : "[",
                    n.min.v,
                    n.max.v,
                    n.max.i == Inclusivity.EXCLUSIVE ? ")" : "]");
            }
            if (first)
            {
                s.Append("(0, 0)");
            }
            return s.ToString();
        }

        public override bool Equals(object other)
        {
            if (other is NumericalValue<T>)
            {
                return Equals(other as NumericalValue<T>);
            }
            return false;
        }

        public bool Equals(NumericalValue<T> other)
        {
            var enumerateThis = ranges.GetEnumerator();
            var enumerateOther = other.ranges.GetEnumerator();
            while (true)
            {
                if (enumerateThis.MoveNext())
                {
                    if (enumerateOther.MoveNext())
                    {
                        if (!enumerateThis.Current.Equals(enumerateOther.Current))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    if (enumerateOther.MoveNext())
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
        }

        /*
        private class Range
        {
            public MinPair min;
            public MaxPair max;

            public Range(MinPair min, MaxPair max)
            {
                this.min = min;
                this.max = max;
            }
        }

        private class MinPair
        {
            T value;
            Inclusivity inclusivity;

            public MinPair(T value, Inclusivity inclusivity)
            {
                this.value = value;
                this.inclusivity = inclusivity;
            }

            public static bool operator==(MinPair self, MinPair other)
            {
                return self.value == other.value && self.inclusivity == other.inclusivity;
            }
            public static bool operator!=(MinPair self, MinPair other) { return self != other; }
        }

        private class MaxPair
        {
            T value;
            Inclusivity inclusivity;

            public MaxPair(T value, Inclusivity inclusivity)
            {
                this.value = value;
                this.inclusivity = inclusivity;
            }
        }
        */
        
        public void UnionWith(NumericalValue<T> other)
        {
            foreach (Range n in other.ranges)
            {
                UnionWith(n);
            }
        }

        private void UnionWith(Range other)
        {
            var removeRanges = new List<Range>();
            Range mergeRange = null;
            foreach (Range range in ranges)
            {
                if (mergeRange != null)
                {
                    // [   ]
                    //     [   ]
                    // ==>
                    // [       ]
                    // remove range
                    if (range.min <= mergeRange.max)
                    {
                        mergeRange.max = range.max;
                        removeRanges.Add(range);
                    }
                    else
                    {
                        break;
                    }
                }
                //    [ ]
                // [       ]
                // ==>
                // [       ]
                else if (other.min <= range.min && range.max <= other.max)
                {
                    range.min = other.min;
                    range.max = other.max;
                    mergeRange = range;
                }
                // [       ]
                //    [ ]
                // ==>
                // no changes
                else if (range.min <= other.min && other.max <= range.max)
                {
                    break;
                }
                // [   ]
                //     [   ]
                // ==>
                // [       ]
                else if (range.min <= other.min && other.min <= range.max)
                {
                    range.max = other.max;
                    mergeRange = range;
                }
                //     [   ]
                // [   ]
                // ==>
                // [       ]
                else if (other.min <= range.min && range.min <= other.max)
                {
                    range.min = other.min;
                    mergeRange = range;
                }
            }

            if (mergeRange == null)
            {
                ranges.Add(other);
            }

            foreach (var range in removeRanges)
            {
                ranges.Remove(range);
            }
        }

        public void AssignValue(NumericalValue<T> other)
        {
            this.ranges = new SortedSet<Range>(other.ranges);
        }

        public void IntersectWith(NumericalValue<T> other)
        {
            NumericalValue<T> output = new NumericalValue<T>();
            foreach (var r in other.ranges)
            {
                NumericalValue<T> c = this.Clone();
                c.IntersectWith(r);
                output.UnionWith(c);
            }
            this.ranges = output.ranges;
        }

        public void IntersectWith(T min, Inclusivity minI, T max, Inclusivity maxI)
        {
            IntersectWith(new Range(new Pair(min, minI), new Pair(max, maxI)));
        }

        private void IntersectWith(Range other)
        {
            var removeRanges = new List<Range>();
            foreach (var range in ranges)
            {
                // [   ]
                //  [ ]
                // ==>
                //  [ ]
                if (range.min <= other.min && range.max >= other.max)
                {
                    range.min = other.min;
                    range.max = other.max;
                }
                //  [ ]
                // [   ]
                // ==>
                // do nothing
                else if (range.min >= other.min && range.max <= other.max) { }
                // [   ]
                //      [   ]
                // ==>
                // remove range
                else if (range.max < other.min)
                {
                    removeRanges.Add(range);
                }
                //      [   ]
                // [   ]
                // ==>
                // remove range
                else if (other.max < range.min)
                {
                    removeRanges.Add(range);
                }
                // [   ]
                //    [   ]
                // ==>
                //    []
                else if (range.min < other.min && range.max >= other.min)
                {
                    range.min = other.min;
                }
                //    [   ]
                // [   ]
                // ==>
                //    []
                else if (other.min < range.min && other.max >= range.min)
                {
                    range.max = other.max;
                }
            }

            foreach (var range in removeRanges)
            {
                ranges.Remove(range);
            }
        }

        private class Range : IComparable<Range>
        {
            public Pair min;
            public Pair max;

            public Range(Pair min, Pair max)
            {
                this.min = min;
                this.max = max;
            }

            public int CompareTo(Range b)
            {
                return this.min.CompareTo(b.min);
            }

            public override bool Equals(object obj)
            {
                if (obj is Range)
                {
                    var range = obj as Range;
                    return min.Equals(range.min) && max.Equals(range.max);
                }
                else
                {
                    return false;
                }
            }

            public Range Clone()
            {
                return new Range(min, max);
            }
        }

        private struct Pair : IComparable<Pair>
        {
            public T v;
            public Inclusivity i;

            public Pair(T v, Inclusivity i)
            {
                this.v = v;
                this.i = i;
            }

            public static bool operator <(Pair a, Pair b)
            {
                return a.CompareTo(b) < 0;
            }
            public static bool operator >(Pair a, Pair b)
            {
                return b < a;
            }

            public static bool operator <=(Pair a, Pair b)
            {
                return !(b < a);
            }
            public static bool operator >=(Pair a, Pair b)
            {
                return !(a < b);
            }

            public int CompareTo(Pair b)
            {
                int c = Comparer<T>.Default.Compare(this.v, b.v);
                if (this.i == Inclusivity.INCLUSIVE && b.i == Inclusivity.EXCLUSIVE && c == 0) { return -1; }
                return c;
            }

            public bool Equals(Pair pair)
            {
                return CompareTo(pair) == 0;
            }
        }
    }

    public enum Inclusivity
    {
        INCLUSIVE, EXCLUSIVE
    }
}