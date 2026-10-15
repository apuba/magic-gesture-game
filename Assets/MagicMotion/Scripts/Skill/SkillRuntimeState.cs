namespace MagicMotion.Skill
{
    public sealed class SkillRuntimeState
    {
        public SkillRuntimeState(SkillDefinition definition)
        {
            Definition = definition;
        }

        public SkillDefinition Definition { get; }
        public SkillState State { get; private set; } = SkillState.Idle;
        public double CooldownUntil { get; private set; }

        public bool TryPrepare(double timestamp)
        {
            Refresh(timestamp);
            if (State != SkillState.Idle)
            {
                return false;
            }

            State = SkillState.Prepare;
            return true;
        }

        public bool TryCast(double timestamp)
        {
            Refresh(timestamp);
            if (State != SkillState.Prepare && State != SkillState.Idle)
            {
                return false;
            }

            State = SkillState.Cast;
            return true;
        }

        public void EnterCooldown(double timestamp)
        {
            State = SkillState.Cooldown;
            CooldownUntil = timestamp + Definition.Cooldown;
        }

        public void Cancel()
        {
            if (State == SkillState.Prepare || State == SkillState.Charging)
            {
                State = SkillState.Idle;
            }
        }

        public bool Refresh(double timestamp)
        {
            if (State == SkillState.Cooldown && timestamp >= CooldownUntil)
            {
                State = SkillState.Idle;
                return true;
            }

            return false;
        }
    }
}

