﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Fitbit.Models;

namespace Fitbit.Api.Portable
{
    public class FitbitClient : IFitbitClient
    {
        /// <summary>
        /// The httpclient which will be used for the api calls through the FitbitClient instance
        /// </summary>
        public HttpClient HttpClient { get; private set; }

        /// <summary>
        /// Use this constructor if an authorized httpclient has already been setup and accessing the resources is what is required.
        /// </summary>
        /// <param name="httpClient"></param>
        public FitbitClient(HttpClient httpClient) : this(string.Empty, string.Empty, string.Empty, string.Empty, httpClient)
        {
        }

        /// <summary>
        /// Use this constructor if the access tokens and keys are known. A httpclient with the correct authorizaton information will be setup to use in the calls.
        /// </summary>
        /// <param name="consumerKey"></param>
        /// <param name="consumerSecret"></param>
        /// <param name="accessToken"></param>
        /// <param name="accessSecret"></param>
        public FitbitClient(string consumerKey, string consumerSecret, string accessToken, string accessSecret) : this(consumerKey, consumerSecret, accessToken, accessSecret, httpClient: null)
        {
            // note: do not remove the httpclient optional parameter above, even if resharper says you should, as otherwise it will make a cyclic constructor call .... which is bad!
        }

        /// <summary>
        /// Private base constructor which takes it all and constructs or throws exceptions as appropriately
        /// </summary>
        /// <param name="consumerKey"></param>
        /// <param name="consumerSecret"></param>
        /// <param name="accessToken"></param>
        /// <param name="accessSecret"></param>
        /// <param name="httpClient"></param>
        private FitbitClient(string consumerKey, string consumerSecret, string accessToken, string accessSecret, HttpClient httpClient = null)
        {
            HttpClient = httpClient;
            if (HttpClient == null)
            {
                #region Parameter checking
                if (string.IsNullOrWhiteSpace(consumerKey))
                {
                    throw new ArgumentNullException("consumerKey", "ConsumerKey must not be empty or null");
                }

                if (string.IsNullOrWhiteSpace(consumerSecret))
                {
                    throw new ArgumentNullException("consumerSecret", "ConsumerSecret must not be empty or null");
                }

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    throw new ArgumentNullException("accessToken", "AccessToken must not be empty or null");
                }

                if (string.IsNullOrWhiteSpace(accessSecret))
                {
                    throw new ArgumentNullException("accessSecret", "AccessSecret must not be empty or null");
                }
                #endregion

                HttpClient = AsyncOAuth.OAuthUtility.CreateOAuthClient(consumerKey, consumerSecret, new AsyncOAuth.AccessToken(accessToken, accessSecret));
            }
        }

        /// <summary>
        /// Requests the devices for the current logged in user
        /// </summary>
        /// <returns>List of <see cref="Device"/></returns>
        public async Task<FitbitResponse<List<Device>>> GetDevicesAsync()
        {
            var apiCall = "/1/user/-/devices.json".ToFullUrl();

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<List<Device>>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var serializer = new JsonDotNetSerializer();
                fitbitResponse.Data = serializer.Deserialize<List<Device>>(responseBody);    
            }
            return fitbitResponse;
        }

        /// <summary>
        /// Requests the friends of the encoded user id or if none supplied the current logged in user
        /// </summary>
        /// <param name="encodedUserId">encoded user id, can be null for current logged in user</param>
        /// <returns>List of <see cref="UserProfile"/></returns>
        public async Task<FitbitResponse<List<UserProfile>>> GetFriendsAsync(string encodedUserId = default(string))
        {
            string apiCall = "/1/user/{0}/friends.json".ToFullUrl(encodedUserId);

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<List<UserProfile>>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var serializer = new JsonDotNetSerializer();
                fitbitResponse.Data = serializer.GetFriends(responseBody);
            }
            return fitbitResponse;
        }

        /// <summary>
        /// Requests the user profile of the encoded user id or if none specified the current logged in user
        /// </summary>
        /// <param name="encodedUserId"></param>
        /// <returns><see cref="UserProfile"/></returns>
        public async Task<FitbitResponse<UserProfile>> GetUserProfileAsync(string encodedUserId = default(string))
        {
            string apiCall = "/1/user/{0}/profile.json".ToFullUrl(encodedUserId);

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<UserProfile>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var serializer = new JsonDotNetSerializer { RootProperty = "user" };
                fitbitResponse.Data = serializer.Deserialize<UserProfile>(responseBody);    
            }
            return fitbitResponse;
        }

        /// <summary>
        /// Requests the specified <see cref="TimeSeriesResourceType"/> for the date range and user specified
        /// </summary>
        /// <param name="timeSeriesResourceType"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="encodedUserId"></param>
        /// <returns></returns>
        public async Task<FitbitResponse<TimeSeriesDataList>> GetTimeSeriesAsync(TimeSeriesResourceType timeSeriesResourceType, DateTime startDate, DateTime endDate, string encodedUserId = default(string))
        {
            return await GetTimeSeriesAsync(timeSeriesResourceType, startDate, endDate.ToFitbitFormat(), encodedUserId);
        }

        /// <summary>
        /// Requests the specified <see cref="TimeSeriesResourceType"/> for the date range and user specified 
        /// </summary>
        /// <param name="timeSeriesResourceType"></param>
        /// <param name="endDate"></param>
        /// <param name="period"></param>
        /// <param name="encodedUserId"></param>
        /// <returns></returns>
        public async Task<FitbitResponse<TimeSeriesDataList>> GetTimeSeriesAsync(TimeSeriesResourceType timeSeriesResourceType, DateTime endDate, DateRangePeriod period, string encodedUserId = default(string))
        {
            return await GetTimeSeriesAsync(timeSeriesResourceType, endDate, period.GetStringValue(), encodedUserId);
        }

        /// <summary>
        /// Requests the specified <see cref="TimeSeriesResourceType"/> for the date range and user specified
        /// </summary>
        /// <param name="timeSeriesResourceType"></param>
        /// <param name="baseDate"></param>
        /// <param name="endDateOrPeriod"></param>
        /// <param name="encodedUserId"></param>
        /// <returns></returns>
        private async Task<FitbitResponse<TimeSeriesDataList>> GetTimeSeriesAsync(TimeSeriesResourceType timeSeriesResourceType, DateTime baseDate, string endDateOrPeriod, string encodedUserId = default(string))
        {
            var apiCall = "/1/user/{0}{1}/date/{2}/{3}.json".ToFullUrl(encodedUserId, timeSeriesResourceType.GetStringValue(), baseDate.ToFitbitFormat(), endDateOrPeriod);

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<TimeSeriesDataList>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var serializer = new JsonDotNetSerializer {RootProperty = timeSeriesResourceType.ToTimeSeriesProperty()};
                fitbitResponse.Data = serializer.GetTimeSeriesDataList(responseBody);   
            }
            return fitbitResponse;
        }

        /// <summary>
        /// Requests the specified <see cref="TimeSeriesResourceType"/> for the date range and user specified
        /// </summary>
        /// <param name="timeSeriesResourceType"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="encodedUserId"></param>
        /// <returns></returns>
        public Task<FitbitResponse<TimeSeriesDataListInt>> GetTimeSeriesIntAsync(TimeSeriesResourceType timeSeriesResourceType, DateTime startDate, DateTime endDate, string encodedUserId = null)
        {
            return GetTimeSeriesIntAsync(timeSeriesResourceType, startDate, endDate.ToFitbitFormat(), encodedUserId);
        }

        /// <summary>
        /// Requests the specified <see cref="TimeSeriesResourceType"/> for the date range and user specified
        /// </summary>
        /// <param name="timeSeriesResourceType"></param>
        /// <param name="endDate"></param>
        /// <param name="period"></param>
        /// <param name="encodedUserId"></param>
        /// <returns></returns>
        public Task<FitbitResponse<TimeSeriesDataListInt>> GetTimeSeriesIntAsync(TimeSeriesResourceType timeSeriesResourceType, DateTime endDate, DateRangePeriod period, string encodedUserId = null)
        {
            return GetTimeSeriesIntAsync(timeSeriesResourceType, endDate, period.GetStringValue(), encodedUserId);
        }

        /// <summary>
        /// Get TimeSeries data for another user accessible with this user's credentials
        /// </summary>
        /// <param name="timeSeriesResourceType"></param>
        /// <param name="baseDate"></param>
        /// <param name="endDateOrPeriod"></param>
        /// <param name="encodedUserId"></param>
        /// <returns></returns>
        private async Task<FitbitResponse<TimeSeriesDataListInt>> GetTimeSeriesIntAsync(TimeSeriesResourceType timeSeriesResourceType, DateTime baseDate, string endDateOrPeriod, string encodedUserId)
        {
            var apiCall = "/1/user/{0}{1}/date/{2}/{3}.json".ToFullUrl(encodedUserId, timeSeriesResourceType.GetStringValue(), baseDate.ToFitbitFormat(), endDateOrPeriod);

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<TimeSeriesDataListInt>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var serializer = new JsonDotNetSerializer { RootProperty = timeSeriesResourceType.ToTimeSeriesProperty() };
                fitbitResponse.Data = serializer.GetTimeSeriesDataListInt(responseBody);
            }
            return fitbitResponse;
        }

        /// <summary>
        /// Get food information for date value and user specified
        /// </summary>
        /// <param name="date"></param>
        /// <param name="encodedUserId"></param>
        /// <returns></returns>
        public async Task<FitbitResponse<Food>> GetFoodAsync(DateTime date, string encodedUserId = default(string))
        {
            string apiCall = "/1/user/{0}/foods/log/date/{1}.json".ToFullUrl(encodedUserId, date.ToFitbitFormat());

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<Food>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var serializer = new JsonDotNetSerializer();
                fitbitResponse.Data = serializer.Deserialize<Food>(responseBody);
            }

            return fitbitResponse;
        }

        /// <summary>
        /// Get blood pressure data for date value and user specified
        /// </summary>
        /// <param name="date"></param>
        /// <param name="encodedUserId"></param>
        /// <returns></returns>
        public async Task<FitbitResponse<BloodPressureData>> GetBloodPressureAsync(DateTime date, string encodedUserId = default(string))
        {
            string apiCall = "/1/user/{0}/bp/date/{1}.json".ToFullUrl(encodedUserId, date.ToFitbitFormat());

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<BloodPressureData>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var serializer = new JsonDotNetSerializer();
                fitbitResponse.Data = serializer.Deserialize<BloodPressureData>(responseBody);
            }

            return fitbitResponse;
        }

        /// <summary>
        /// Get the set body measurements for the date value and user specified
        /// </summary>
        /// <param name="date"></param>
        /// <param name="encodedUserId"></param>
        /// <returns></returns>
        public async Task<FitbitResponse<BodyMeasurements>> GetBodyMeasurementsAsync(DateTime date, string encodedUserId = default(string))
        {
            string apiCall = "/1/user/{0}/body/date/{1}.json".ToFullUrl(encodedUserId, date.ToFitbitFormat());
            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<BodyMeasurements>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var serializer = new JsonDotNetSerializer();
                fitbitResponse.Data = serializer.Deserialize<BodyMeasurements>(responseBody);
            }
            return fitbitResponse;
        }

        /// <summary>
        /// Get Fat for a period of time starting at date.
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public async Task<FitbitResponse<Fat>> GetFatAsync(DateTime startDate, DateRangePeriod period)
        {
            switch (period)
            {
                case DateRangePeriod.OneDay:
                case DateRangePeriod.SevenDays:
                case DateRangePeriod.OneWeek:
                case DateRangePeriod.ThirtyDays:
                case DateRangePeriod.OneMonth:
                    break;

                default:
                    throw new Exception("This API endpoint only supports range up to 31 days. See https://wiki.fitbit.com/display/API/API-Get-Body-Fat");
            }

            string apiCall = "/1/user/{0}/body/log/fat/date/{1}/{2}.json".ToFullUrl(args: new object[]{startDate.ToFitbitFormat(), period.GetStringValue()});

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<Fat>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var seralizer = new JsonDotNetSerializer();
                fitbitResponse.Data = seralizer.GetFat(responseBody);
            }

            return fitbitResponse;
        }

        /// <summary>
        /// Get Fat for a specific date or a specific period between two dates
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<FitbitResponse<Fat>> GetFatAsync(DateTime startDate, DateTime? endDate = null)
        {
            string apiCall = string.Empty;
            if (endDate == null)
            {
                apiCall = "/1/user/{0}/body/log/fat/date/{1}.json".ToFullUrl(args: new object[] { startDate.ToFitbitFormat()});
            }
            else
            {
                if (startDate.AddDays(31) < endDate)
                {
                    throw new Exception("31 days is the max span. Try using period format instead for longer: https://wiki.fitbit.com/display/API/API-Get-Body-Fat");
                }

                apiCall = "/1/user/{0}/body/log/fat/date/{1}/{2}.json".ToFullUrl(args: new object[]{ startDate.ToFitbitFormat(), endDate.Value.ToFitbitFormat()});
            }

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<Fat>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var seralizer = new JsonDotNetSerializer();
                fitbitResponse.Data = seralizer.GetFat(responseBody);
            }

            return fitbitResponse;
        }

        /// <summary>
        /// Get Fat for a period of time starting at date.
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public async Task<FitbitResponse<Weight>> GetWeightAsync(DateTime startDate, DateRangePeriod period)
        {
            switch (period)
            {
                case DateRangePeriod.OneDay:
                case DateRangePeriod.SevenDays:
                case DateRangePeriod.OneWeek:
                case DateRangePeriod.ThirtyDays:
                case DateRangePeriod.OneMonth:
                    break;

                default:
                    throw new Exception("This API endpoint only supports range up to 31 days. See https://wiki.fitbit.com/display/API/API-Get-Body-Weight");
            }

            string apiCall = "/1/user/{0}/body/log/weight/date/{1}/{2}.json".ToFullUrl(args: new object[] { startDate.ToFitbitFormat(), period.GetStringValue() });

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<Weight>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var seralizer = new JsonDotNetSerializer();
                fitbitResponse.Data = seralizer.GetWeight(responseBody);
            }

            return fitbitResponse;
        }

        /// <summary>
        /// Get Weight for a specific date or a specific period between two dates
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<FitbitResponse<Weight>> GetWeightAsync(DateTime startDate, DateTime? endDate = null)
        {
            string apiCall = string.Empty;
            if (endDate == null)
            {
                apiCall = "/1/user/{0}/body/log/weight/date/{1}.json".ToFullUrl(args: new object[] { startDate.ToFitbitFormat() });
            }
            else
            {
                if (startDate.AddDays(31) < endDate)
                {
                    throw new Exception("31 days is the max span. Try using period format instead for longer: https://wiki.fitbit.com/display/API/API-Get-Body-Weight");
                }

                apiCall = "/1/user/{0}/body/log/weight/date/{1}/{2}.json".ToFullUrl(args: new object[] { startDate.ToFitbitFormat(), endDate.Value.ToFitbitFormat() });
            }

            HttpResponseMessage response = await HttpClient.GetAsync(apiCall);
            var fitbitResponse = await HandleResponse<Weight>(response);
            if (fitbitResponse.Success)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var seralizer = new JsonDotNetSerializer();
                fitbitResponse.Data = seralizer.GetWeight(responseBody);
            }

            return fitbitResponse;
        }

        /// <summary>
        /// General error checking of the response before specific processing is done.
        /// </summary>
        /// <param name="response"></param>
        private async Task<FitbitResponse<T>> HandleResponse<T>(HttpResponseMessage response) where T : class
        {
            var errors = new List<ApiError>();
            
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var serializer = new JsonDotNetSerializer { RootProperty = "errors" };
                    errors.AddRange(serializer.Deserialize<List<ApiError>>(await response.Content.ReadAsStringAsync()));
                }
                catch
                {
                    // if there is an error with the serialization then we need to default the errors back to an instantiated list
                    errors = new List<ApiError>();
                }  
            }

            // todo: handle "success" responses which return errors?

            return new FitbitResponse<T>(response.StatusCode, response.Headers, errors);
        }
    }
}