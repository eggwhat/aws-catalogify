using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cataloguify.Entities
{
    public class PagedResponse<T>
    {
        public int Page { get; set; }
        public int Results { get; set; }
        public int TotalPages { get; set; }
        public T Content { get; set; }
    }
}
