using System;

namespace AzerothCoreLauncher
{
    public class ScheduledEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Command"; // Command, Restart, Announcement, etc.
        public string Target { get; set; } = "World"; // Auth, World
        public string Command { get; set; } = string.Empty;
        public TimeSpan ScheduledTime { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsRecurring { get; set; } = false; // Daily, Weekly, etc.
        public string RecurrencePattern { get; set; } = "Daily"; // Daily, Weekly, Monthly
        public DateTime? LastExecuted { get; set; }
        public DateTime? NextExecution { get; set; }
    }
}
