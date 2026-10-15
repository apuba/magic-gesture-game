using System.Collections.Generic;
using MagicMotion.Vision;

namespace MagicMotion.Gesture
{
    public sealed class GestureStabilizer
    {
        private sealed class HandState
        {
            public GestureType Candidate;
            public GestureType Emitted;
            public int ConsecutiveFrames;
            public int MissingFrames;
        }

        private readonly int requiredFrames;
        private readonly int requiredMissingFrames;
        private readonly Dictionary<Handedness, HandState> states =
            new Dictionary<Handedness, HandState>();

        public GestureStabilizer(int requiredFrames, int requiredMissingFrames = 3)
        {
            this.requiredFrames = requiredFrames < 1 ? 1 : requiredFrames;
            this.requiredMissingFrames = requiredMissingFrames < 1 ? 1 : requiredMissingFrames;
        }

        public bool TryConfirm(Handedness hand, GestureType gesture, out GestureType confirmed)
        {
            if (gesture == GestureType.Unknown)
            {
                confirmed = GestureType.Unknown;
                return false;
            }

            if (!states.TryGetValue(hand, out var state))
            {
                state = new HandState();
                states.Add(hand, state);
            }

            state.MissingFrames = 0;

            if (state.Candidate != gesture)
            {
                state.Candidate = gesture;
                state.ConsecutiveFrames = 1;
            }
            else
            {
                state.ConsecutiveFrames++;
            }

            if (state.ConsecutiveFrames >= requiredFrames && state.Emitted != gesture)
            {
                state.Emitted = gesture;
                confirmed = gesture;
                return true;
            }

            confirmed = GestureType.Unknown;
            return false;
        }

        public void Reset(Handedness hand)
        {
            states.Remove(hand);
        }

        public bool TryReset(Handedness hand, out GestureType previous)
        {
            if (states.TryGetValue(hand, out var state) && state.Emitted != GestureType.Unknown)
            {
                previous = state.Emitted;
                states.Remove(hand);
                return true;
            }

            previous = GestureType.Unknown;
            states.Remove(hand);
            return false;
        }

        public bool TryResetAfterMissing(Handedness hand, out GestureType previous)
        {
            if (!states.TryGetValue(hand, out var state))
            {
                previous = GestureType.Unknown;
                return false;
            }

            state.MissingFrames++;
            if (state.MissingFrames < requiredMissingFrames)
            {
                previous = GestureType.Unknown;
                return false;
            }

            return TryReset(hand, out previous);
        }

        public void ResetAll()
        {
            states.Clear();
        }
    }
}

