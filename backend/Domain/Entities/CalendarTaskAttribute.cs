using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public sealed class CalendarTaskAttribute
    {
        public Guid CalendarTaskId { get; set; }
        public AttributeType AttributeType { get; set; }
        public int SharePercent { get; set; }

        public CalendarTask CalendarTask { get; set; } = default!;
    }
}
