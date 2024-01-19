using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nami
{
    public class Configuration
    {
        public string Address;
        public int Port;
        public string Root;
        public List<Route> Routes;

        public Configuration()
        {
            Address = string.Empty;
            Port = 0;
            Root = string.Empty;
            Routes = new List<Route>();
        }
    }
}
