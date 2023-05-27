﻿using Microsoft.AspNetCore.Mvc;
using Refit;

namespace PollyDemo.Services
{
    public interface IService1
    {
        [Get("/200?sleep={delay}")]
        public Task<IApiResponse> GetWithDelayAsync(int delay, CancellationToken cancellationToken);

        [Get("/500")]
        public Task<IApiResponse> GetWithErrorAsync(CancellationToken cancellationToken);
    }
}
