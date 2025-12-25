using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestTask.Models
{
    public class Response
    {
        public int StatusCode { get; set; }
        public string Body { get; set; }

        public Response(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body;
        }
    }
}
