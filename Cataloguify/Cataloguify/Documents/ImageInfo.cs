using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cataloguify.Documents
{
    [DynamoDBTable("imageinfos")]
    public class ImageInfo
    {
        [DynamoDBHashKey] 
        public Guid ImageKey { get; set; }

        [DynamoDBProperty]
        public Guid UserId { get; set; }

        [DynamoDBProperty]
        public List<string> Tags { get; set; }
    }
}
