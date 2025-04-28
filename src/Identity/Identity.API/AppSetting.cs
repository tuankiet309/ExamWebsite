using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Identity.API
{
    public class AppSettings
    {
        public string ExamWebAppClient { set; get; }
        public string ExamWebAppAdminClient { set; get; }

        public string ExamWebApiClient { set; get; }
    }

}