namespace ExtremeClipping
{
    public abstract class Module
    {
        public virtual bool IsEnabled
        {
            get => true;
            set => _ = value;
        }
        public virtual int DrawOrder => 0;

        // Shown on the Overview tab. Keep this to the one or two controls people reach for often.
        public virtual bool HasOverview => false;
        public virtual void DrawConfig() { }

        // Shown on the Settings tab: everything that isn't a daily control.
        public virtual void DrawSettings() { }
        public virtual void Enable() { }
        public virtual void Disable() { }
    }
}
