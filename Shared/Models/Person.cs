namespace StallmedManager.Shared.Models
{
    public class Person 
    {
        public int id { get; set; }
        public string PhName { get; set; }
        public string DocName { get; set; }
        public string PAtName { get; set; }
        public string Treatment { get; set; }
        public int Qnt { get; set; }
        public DateTime OrderDate {  get; set; } 
    }    
}