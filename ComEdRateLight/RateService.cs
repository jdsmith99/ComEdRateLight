using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace ComEdRateLight
{
    public class RateService
    {
        static string apiUri = "https://hourlypricing.comed.com/api?type=5minutefeed&datestart={0}&dateend={1}";
        //static string endTime = "220012312359";

        public static async Task<List<Rate>> GetRate(DateTime Time)
        //public static async Task<Rate[]> GetRate(string Time)
        {

            var rightNow = Time.AddMinutes(-10);
            var endTime = rightNow.AddDays(1);
            var parmFormat = "{0}{1}{2}{3}{4}";
            var dateParm = string.Format(parmFormat, rightNow.Year, rightNow.Month.ToString("D2"), rightNow.Day.ToString("D2"), rightNow.Hour.ToString("D2"), rightNow.Minute.ToString("D2"));
            var endParm = string.Format(parmFormat, endTime.Year, endTime.Month.ToString("D2"), endTime.Day.ToString("D2"), endTime.Hour.ToString("D2"), endTime.Minute.ToString("D2"));

            using (var client = new HttpClient())
            {
                string replUri = string.Format(apiUri, dateParm, endParm);
                HttpResponseMessage response = await client.GetAsync(replUri);
                if (response.IsSuccessStatusCode)
                {
                    string resultTemplate = "\"results\":{0}";
                    string result = await response.Content.ReadAsStringAsync();
                    string resultMod = "{" + string.Format(resultTemplate, result) + "}";
                    try
                    {
                        var rootResult = JsonConvert.DeserializeObject<RootObject>(resultMod);
                        return rootResult.results;

                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        var exceptionMsg = ex.Message;
                        return null;

                    }

                }
                else
                {
                    return null;
                }

            }
        }
    }
}
