using System;
using System.Collections.Generic;

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
        public bool IsRecurring { get; set; } = false;
        
        // Recurrence Options
        public string RecurrencePattern { get; set; } = "Daily"; // Daily, Weekly, Monthly, Custom
        public int RecurrenceInterval { get; set; } = 1; // For custom: every X days
        public List<DayOfWeek> RecurrenceDays { get; set; } = new List<DayOfWeek>(); // For weekly: which days
        public int? DayOfMonth { get; set; } = null; // For monthly: which day (1-31)
        
        // Event Chaining
        public List<string> ChainedEventIds { get; set; } = new List<string>(); // IDs of events to execute after this one
        public int ChainDelaySeconds { get; set; } = 0; // Delay before executing chained events
        
        // Conditional Execution
        public bool HasConditions { get; set; } = false;
        public int? MinPlayerCount { get; set; } = null; // Execute only if player count >= this
        public int? MaxPlayerCount { get; set; } = null; // Execute only if player count <= this
        public TimeSpan? StartTimeWindow { get; set; } = null; // Execute only after this time
        public TimeSpan? EndTimeWindow { get; set; } = null; // Execute only before this time
        public List<string> RequiredServerStates { get; set; } = new List<string>(); // e.g., "Running", "Online"
        
        public DateTime? LastExecuted { get; set; }
        public DateTime? NextExecution { get; set; }
    }
}
