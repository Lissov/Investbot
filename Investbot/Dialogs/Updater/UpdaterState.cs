using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Investbot.Dialogs.Updater
{
    public class UpdaterState
    {
        public Timex.Timex UpdateDateTime { get; set; }
        public string Action { get; set; }
        public string Detalization { get; set; }
    }
}
