#region (c) 2022 Gilles Macabies All right reserved

// Author     : Gilles Macabies
// Solution   : FilterDataGrid
// Projet     : DemoApp.Net6.0
// File       : Employe.cs
// Created    : 13/11/2022
// 

#endregion


using System;

// ReSharper disable CheckNamespace
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable TooManyDependencies
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable ConvertToAutoProperty
// ReSharper disable ArrangeAccessorOwnerBody
// ReSharper disable MemberCanBePrivate.Global

namespace SharedModelView
{
    public class Address
    {
        #region Public Constructors

        public Address()
        {
            
        }

        public Address(string street, int houseNumber, string postalCode)
        {
            Street = street;
            HouseNumber = houseNumber;
            PostalCode = postalCode;
        }

        #endregion Public Constructors

        #region Public Properties

        public string Street { get; set; }
        public int HouseNumber { get; set; }
        public string PostalCode { get; set; }

        #endregion Public Properties
    }
}