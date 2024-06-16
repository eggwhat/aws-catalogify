using Cataloguify.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cataloguify.Dynamo
{
    public interface IDynamoDBHelper
    {
        public Task<User> GetUserByEmailAsync(string email);
        public Task<IEnumerable<ImageInfo>> GetUserImagesAsync(string userId);
    }
}
