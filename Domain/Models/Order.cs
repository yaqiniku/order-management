namespace Domain.Models
{
	public class Order
	{
		public string? ID { get; set; }
		public string? CustomerID { get; set; }
		public string? Status { get; set; }
		public string? ShippingAddress { get; set; }
		public decimal? TotalAmount { get; set; }
		public string? IdempotencyKey { get; set; }
        public DateTime? CreDate { get; set; }
        public DateTime? ModDate { get; set; }
		public List<OrderDetail> Items { get; set; } = [];
    }
}
