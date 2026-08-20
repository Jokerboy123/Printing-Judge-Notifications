using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Printing_Judge_Notifications
{
    public class OwnerData
    {
    /* A */ public string CourtOfficer { get; set; } = "";/**/   // Судебный Пристав и иная информация
    /* B */ public DateTime? DateOfVerification { get; set; } // Дата проверки информации...
    /* C */ public string NumberOfJudicalArea { get; set; } = "";// № Судебного участка
    /* D */ public string Account { get; set; } = "";    /**//**/// Лицевой счет
    /* E */ public string FullName { get; set; } = "";   
    /* F */ public string Part { get; set; } = "";
    /* G */ public string Town { get; set; } = "";
    /* H */ public string Street { get; set; } = "";
    /* I */ public string Building { get; set; } = "";
    /* J */ public string Korps { get; set; } = "";
    /* K */ public string Flat { get; set; } = "";
    /* L */ public string Room { get; set; } = "";
    /* M */ public string RequestMCDate { get; set; } = "";
    /* N */ public string OrderNumber { get; set; } = "";
    /* O */ public DateTime? OrderDate { get; set; }
    /* P */ public DateTime? TransferDateFSSP { get; set; }
    /* Q */ public DateTime? InitiationDate { get; set; }
    /* R */ public string Appendex { get; set; } = "";
    /* S */ public DateTime? BirthDate { get; set; }
    /* T */ public string Period { get; set; } = "";
    /* U */ public decimal? Debt { get; set; } = 0m;
    /* V */ public decimal? Punishment { get; set; } = 0m;
    /* W */ public decimal? Duty { get; set; } = 0m;
    /* X */ public decimal? Valuation { get; set; } = 0m; // Оценка имущества
    /* Y */ public decimal? AmountSum { get; set; } = 0m;
    /* Z */ public string Document { get; set; } = "";



    }

}

