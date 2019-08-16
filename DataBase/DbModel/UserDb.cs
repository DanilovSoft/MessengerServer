using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace DbModel
{
    public sealed class UserDb
    {
        public int Id;
        public string Login;
        public string Password;
        public string ImageUrl;
    }
}
