namespace CSI_Assessment_Zone.Models
{
    public class JournalEntry
    {
        public string Rrn { get; set; }
        public string Stan { get; set; }
        public string AcquirerBank { get; set; }
        public int Amount { get; set; }
        public string AccountNumber { get; set; }
        public string Pan { get; set; }
        public string TransactionStatus { get; set; }
        public string CurrencyCode { get; set; }
        public string Comment { get; set; }
        public string TransactionDate { get; set; }
        public string TransactionTime { get; set; }
        public string Error { get; set; }
        public string TerminalId { get; set; }
    }
}
