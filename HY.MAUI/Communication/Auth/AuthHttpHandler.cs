using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HY.MAUI.Communication.Auth
{
    public class AuthHttpHandler : DelegatingHandler
    {
        private readonly ITokenProvider _tokenProvider;
        private readonly IAuthService _authService;

        public AuthHttpHandler(ITokenProvider tokenProvider, IAuthService authService)
        {
            _tokenProvider = tokenProvider;
            _authService = authService;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Ensure request content is buffered so we can retry safely
            if (request.Content != null)
            {
                var buffered = await CloneHttpContentAsync(request.Content).ConfigureAwait(false);
                request.Content = buffered;
            }

            //await AttachHeaderAccept(request);
            await AttachTokenAsync(request).ConfigureAwait(false);

            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Preserve cancellation
                throw;
            }
            catch (HttpRequestException ex)
            {
                // 网络错误：目标主机不可达/连接被拒绝等
                // 返回 503 以便调用方可以识别为临时不可用
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    ReasonPhrase = "Network error: " + ex.Message
                };
            }

            if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

            var failedAccessToken = request.Headers.Authorization?.Parameter;

            response.Dispose();

            var refreshed = await _authService.RefreshTokenAsync(failedAccessToken).ConfigureAwait(false);
            if (!refreshed)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            var retry = await CloneRequestAsync(request).ConfigureAwait(false);

            await AttachTokenAsync(retry).ConfigureAwait(false);

            try
            {
                return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    ReasonPhrase = "Network error: " + ex.Message
                };
            }
        }

        private async Task AttachTokenAsync(HttpRequestMessage request)
        {
            var token = await _tokenProvider.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        //private async Task AttachHeaderAccept(HttpRequestMessage request)
        //{
        //    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        //    await Task.CompletedTask;
        //}

        private async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version
            };

            if (request.Content != null)
            {
                clone.Content = await CloneHttpContentAsync(request.Content).ConfigureAwait(false);
            }

            foreach (var h in request.Headers) clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

            return clone;
        }

        private static async Task<HttpContent?> CloneHttpContentAsync(HttpContent? content)
        {
            if (content == null) return null;

            // Read as bytes and create a new ByteArrayContent so it can be sent multiple times
            var bytes = await content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var clone = new ByteArrayContent(bytes);

            // copy headers (e.g. content-type)
            foreach (var h in content.Headers)
            {
                clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            return clone;
        }





    }

}
