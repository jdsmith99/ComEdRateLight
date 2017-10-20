using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComEdRateLight
{
    public class Rate
    {
        public string millisUTC { get; set; }
        public string price { get; set; }

    }

    public class RootObject
    {
        public List<Rate> results { get; set; }
        //public Rate[] results { get; set; }

    }
}
