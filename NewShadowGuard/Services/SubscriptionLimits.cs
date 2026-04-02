namespace NewShadowGuard.Services
{
    public static class SubscriptionLimits
    {
        public static SubscriptionLimit GetLimits(string plan)
        {
            return plan.ToLower() switch
            {
                "basic" => new SubscriptionLimit
                {
                    PlanName = "Basic",
                    MaxAssets = 10,
                    MaxUsers = 5,
                    MaxIncidents = 100,
                    MaxLogRetentionDays = 30,
                    SupportLevel = "Email"
                },
                "professional" => new SubscriptionLimit
                {
                    PlanName = "Professional",
                    MaxAssets = 50,
                    MaxUsers = 20,
                    MaxIncidents = 1000,
                    MaxLogRetentionDays = 90,
                    SupportLevel = "Priority"
                },
                "enterprise" => new SubscriptionLimit
                {
                    PlanName = "Enterprise",
                    MaxAssets = -1, // -1 = без ограничений
                    MaxUsers = -1,
                    MaxIncidents = -1,
                    MaxLogRetentionDays = 365,
                    SupportLevel = "24/7"
                },
                _ => new SubscriptionLimit
                {
                    PlanName = "Basic",
                    MaxAssets = 10,
                    MaxUsers = 5,
                    MaxIncidents = 100,
                    MaxLogRetentionDays = 30,
                    SupportLevel = "Email"
                }
            };
        }

        public static bool HasUnlimitedAssets(string plan) => plan.ToLower() == "enterprise";
        public static bool HasUnlimitedUsers(string plan) => plan.ToLower() == "enterprise";
    }

    public class SubscriptionLimit
    {
        public string PlanName { get; set; }
        public int MaxAssets { get; set; }
        public int MaxUsers { get; set; }
        public int MaxIncidents { get; set; }
        public int MaxLogRetentionDays { get; set; }
        public string SupportLevel { get; set; }
    }
}