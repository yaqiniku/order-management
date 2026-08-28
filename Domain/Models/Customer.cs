namespace Domain.Models
{
	public class Customer
	{
		public string? ID { get; set; }
		public string? FullName { get; set; }
		public string? Email { get; set; }
		public string? PhoneNo { get; set; }
		public string? Address { get; set; }
        public DateTime? CreDate { get; set; }
    }
}
