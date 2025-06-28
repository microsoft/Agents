using WHOAMI.Models;

namespace WHOAMI
{
    public class RepairData
    {
        public static List<RepairModel> GetRepairs()
        {
            return new List<RepairModel>
            {
                new() {
                    Id = "1",
                    Title = "Oil change",
                    Description = "Need to drain the old engine oil and replace it with fresh oil to keep the engine lubricated and running smoothly.",
                    AssignedTo = "Karin Blair",
                    Date = "2023-05-23",
                    Image = "https://www.howmuchisit.org/wp-content/uploads/2011/01/oil-change.jpg"
                },
                new() {
                    Id = "2",
                    Title = "Brake repairs",
                    Description = "Conduct brake repairs, including replacing worn brake pads, resurfacing or replacing brake rotors, and repairing or replacing other components of the brake system.",
                    AssignedTo = "Issac Fielder",
                    Date = "2023-05-24",
                    Image = "https://upload.wikimedia.org/wikipedia/commons/thumb/7/71/Disk_brake_dsc03680.jpg/320px-Disk_brake_dsc03680.jpg"
                },
                new() {
                    Id = "3",
                    Title = "Tire service",
                    Description = "Rotate and replace tires, moving them from one position to another on the vehicle to ensure even wear and removing worn tires and installing new ones.",
                    AssignedTo = "Karin Blair",
                    Date = "2023-05-24",
                    Image = "https://th.bing.com/th/id/OIP.N64J4jmqmnbQc5dHvTm-QAHaE8?pid=ImgDet&rs=1"
                },
                new() {
                    Id = "4",
                    Title = "Battery replacement",
                    Description = "Remove the old battery and install a new one to ensure that the vehicle start reliably and the electrical systems function properly.",
                    AssignedTo = "Ashley McCarthy",
                    Date ="2023-05-25",
                    Image = "https://i.stack.imgur.com/4ftuj.jpg"
                },
                new() {
                    Id = "5",
                    Title = "Engine tune-up",
                    Description = "This can include a variety of services such as replacing spark plugs, air filters, and fuel filters to keep the engine running smoothly and efficiently.",
                    AssignedTo = "Karin Blair",
                    Date = "2023-05-28",
                    Image = "https://th.bing.com/th/id/R.e4c01dd9f232947e6a92beb0a36294a5?rik=P076LRx7J6Xnrg&riu=http%3a%2f%2fupload.wikimedia.org%2fwikipedia%2fcommons%2ff%2ff3%2f1990_300zx_engine.jpg&ehk=f8KyT78eO3b%2fBiXzh6BZr7ze7f56TWgPST%2bY%2f%2bHqhXQ%3d&risl=&pid=ImgRaw&r=0"
                },
                new() {
                    Id = "6",
                    Title = "Suspension and steering repairs",
                    Description = "This can include repairing or replacing components of the suspension and steering systems to ensure that the vehicle handles and rides smoothly.",
                    AssignedTo = "Daisy Phillips",
                    Date = "2023-05-29",
                    Image = "https://i.stack.imgur.com/4v5OI.jpg"
                }
            };
        }
    }
}
