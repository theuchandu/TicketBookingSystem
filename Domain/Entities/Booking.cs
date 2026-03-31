using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{

    public class Booking
    {

        public int Id { get; set; }

        public int SeatId { get; set; } 

        public String UserId { get; set; } = string.Empty;

        public DateTime BookedAt { get; set; } = DateTime.Now;

        public BookingStatus Status { get; set; } = BookingStatus.Pending;
    }

    public enum BookingStatus
    {
        Pending,
        Confirmed,
        Failed
    }

}
