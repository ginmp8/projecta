using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Projecta.Json
{
    public sealed class ToObjectSettings
    {
        internal static readonly ToObjectSettings DefaultSettings = new ToObjectSettings();

        public bool AllowMissingProperties { get; set; } = false;
        public bool AllowUndecaredProperties { get; set; } = false;
    }
}
