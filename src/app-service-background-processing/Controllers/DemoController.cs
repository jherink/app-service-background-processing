using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace app_service_background_processing.Controllers
{
    [ApiController]
    [Route( "[controller]" )]
    public class DemoController : BaseController
    {
        [HttpGet("HelloWorld")]
        public string HelloWorld( )
        {
            return "Hello World";
        }

        [HttpPost( "LongRunningSync" )]
        public async Task<string> LongRunningSync( )
        {
            await Task.Delay( 5000 );
            return "Hello World: Sync";
        }

        [HttpPost( "LongRunningAsync" )]
        public void LongRunningAsync( ) 
        {
            base.CreateTask( ( ) =>
            {
                Task.Delay( 20000 ).Wait(); // very long running operation for the sake of the example.
                return null;
            }, 5 );
        }
    }
}
