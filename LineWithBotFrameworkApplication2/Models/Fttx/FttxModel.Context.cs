﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LineWithBotFrameworkApplication2.Models.Fttx
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class TFTTxEntities : DbContext
    {
        public TFTTxEntities()
            : base("name=TFTTxEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<C_SM_INAC> C_SM_INAC { get; set; }
        public virtual DbSet<OCRD> OCRD { get; set; }
        public virtual DbSet<OINV> OINV { get; set; }
        public virtual DbSet<OITM> OITM { get; set; }
        public virtual DbSet<C_PLANBDATA> C_PLANBDATA { get; set; }
    }
}
