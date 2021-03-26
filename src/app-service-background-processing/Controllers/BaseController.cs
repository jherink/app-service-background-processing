using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace app_service_background_processing.Controllers
{
    /// <summary>
    /// Base controller for API Controllers.
    /// </summary>
    public class BaseController : Controller
    {
        /// <summary>
        /// State dictionary for long running HTTP 202 tasks.
        /// </summary>
        private static Dictionary<Guid, bool> RunningTasks = new Dictionary<Guid, bool>();
        private static Dictionary<Guid, object> Results = new Dictionary<Guid, object>();
        private Guid? _activeId { get; set; }

        /// <summary>
        /// Method to check the status of the renew subscriptions job.  This is where the location header redirects to.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet( "TaskStatus" )]
        public async Task TaskStatus( [FromQuery] string id )
        {
            var guidId = Guid.Parse( id );

            //If the job is complete
            if ( RunningTasks.ContainsKey( guidId ) && RunningTasks[ guidId ] )
            {
                RunningTasks.Remove( guidId );
                if ( Results[ guidId ] != null )
                {
                    byte[ ] data = Encoding.UTF8.GetBytes( Newtonsoft.Json.JsonConvert.SerializeObject( Results[ guidId ] ) );
                    await HttpContext.Response.BodyWriter.WriteAsync( data );
                }
                Results[ guidId ] = null;
                HttpContext.Response.StatusCode = ( int )HttpStatusCode.OK;
            }
            //If the job is still running
            else if ( RunningTasks.ContainsKey( guidId ) )
            {
                var builder = new UriBuilder();
                builder.Scheme = Request.Scheme;
                builder.Host = Request.Host.Value;
                builder.Path = $"api/{GetControllerName()}/taskstatus";
                builder.Query = $"id={id}";

                var location = builder.ToString().Replace( "[", string.Empty ).Replace( "]", string.Empty );
                HttpContext.Response.Headers.Add( "location", location );  //Where the engine will poll to check status
                HttpContext.Response.Headers.Add( "retry-after", "5" );   //How many seconds it should wait (20 is default if not included)

                HttpContext.Response.StatusCode = ( int )HttpStatusCode.Accepted;
            }
            else
            {
                HttpContext.Response.StatusCode = ( int )HttpStatusCode.BadRequest;
            }
        }

        protected void CreateTask( Func<object> action, int retryInterval = 20 )
        {
            var id = Guid.NewGuid();  //Generate tracking Id
            RunningTasks.Add( id, false ); //Job isn't done yet
            Results.Add( id, null );

            new Thread( ( ) => DoTask( action, id ) ).Start();

            var builder = new UriBuilder();
            builder.Scheme = Request.Scheme;
            builder.Host = Request.Host.Value;
            builder.Path = $"{GetControllerName()}/taskstatus";
            builder.Query = $"id={id}";

            var location = builder.ToString().Replace( "[", string.Empty ).Replace( "]", string.Empty );
            HttpContext.Response.Headers.Add( "location", location );  //Where the engine will poll to check status
            HttpContext.Response.Headers.Add( "retry-after", retryInterval.ToString() );   //How many seconds it should wait (20 is default if not included)

            HttpContext.Response.StatusCode = ( int )HttpStatusCode.Accepted;
        }

        private void DoTask( Func<object> action, Guid id )
        {
            Results[ id ] = action.Invoke();
            RunningTasks[ id ] = true;
        }

        private string GetControllerName( )
        {
            var version = string.Empty;
            if ( this.ControllerContext.RouteData.Values.TryGetValue( "version", out object routeVersion ) )
            {
                version = $"v{routeVersion}/";
            }
            var controller = this.ControllerContext.RouteData.Values[ "controller" ].ToString();
            return $"{version}{controller}";
        }
    }

}
