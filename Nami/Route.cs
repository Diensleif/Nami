using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nami
{
    public class Route
    {
        public string Pattern;
        public string Type;
        public string Source;

        public Route()
        {
            Pattern = string.Empty;
            Type = string.Empty;
            Source = string.Empty;
        }
    }
}
