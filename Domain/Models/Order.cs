namespace Domain.Models
{
	public class Order
	{
		public string? ID { get; set; }
		public string? CustomerID { get; set; }
		public string? Status { get; set; }
		public string? ShippingAddress { get; set; }
		public decimal? TotalAmount { get; set; }
        public DateTime? CreDate { get; set; }
        public DateTime? ModDate { get; set; }
    }
}
