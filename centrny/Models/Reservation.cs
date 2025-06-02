using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class Reservation
{
    public int ReservationCode { get; set; }

    public int TeacherCode { get; set; }

    public int Capacity { get; set; }

    public string Description { get; set; } = null!;

    public decimal Cost { get; set; }

    public int Period { get; set; }

    public int Deposit { get; set; }

    public DateTime RTime { get; set; }

    public int FinalCost { get; set; }

    public int HallCode { get; set; }

    public virtual Hall HallCodeNavigation { get; set; } = null!;

    public virtual Teacher TeacherCodeNavigation { get; set; } = null!;
}
