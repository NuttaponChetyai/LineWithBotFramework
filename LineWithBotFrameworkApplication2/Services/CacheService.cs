﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LineWithBotFrameworkApplication2.Services
{
    public static class CacheService
    {
        public static Dictionary<string, object> caches;

        static CacheService()
        {
            caches = new Dictionary<string, object>();
        }
    }
}