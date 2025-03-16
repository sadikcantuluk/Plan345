namespace PlanYonetimAraclari.Models
{
    public class InvitationEmailModel
    {
        public string LogoUrl { get; set; }
        public string ProjectName { get; set; }
        public string InviterName { get; set; }
        public int TeamMemberCount { get; set; }
        public string AcceptUrl { get; set; }
        public string DeclineUrl { get; set; }
    }
} 