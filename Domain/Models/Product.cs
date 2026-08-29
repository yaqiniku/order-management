namespace Domain.Models
{
	public class Product
	{
		public string? ID { get; set; }
		public int? Quantity { get; set; }
		public decimal? Price { get; set; }
		public string? ProductName { get; set; }
        public DateTime? CreDate { get; set; }
        public DateTime? ModDate { get; set; }
    }
}
