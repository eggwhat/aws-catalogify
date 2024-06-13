using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cataloguify.Requests
{
    public class SearchImagesRequest
    {
        public List<string> Tags { get; set; }
        public int Page { get; set; }
        public int Results { get; set; }
    }
}
