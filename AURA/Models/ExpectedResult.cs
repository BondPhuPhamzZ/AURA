namespace AURA.Models
{
    public class ExpectedResult
    {
        public string Id { get; set; } = string.Empty;
        public string ExpectedStatus { get; set; } = string.Empty;
        public string ImageName { get; set; } = string.Empty;
        public decimal ClaimedAmount { get; set; }
    }
}
