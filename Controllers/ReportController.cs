using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebCommonFunction;
using Rotativa;
using Rotativa.Options;
using System.Linq.Dynamic;
using System.IO;
//using OfficeOpenXml.Style;
//using OfficeOpenXml;


using IPQC.Models;
using CenterUtil;

namespace IPQC.Controllers
{
    public class ReportController : Controller
    {
        //
        // GET: /Report/
        IPQCEntities db = new IPQCEntities();
        TNC_ADMINEntities tnc = new TNC_ADMINEntities();
        public ActionResult Index()
        {
            return View();
        }

        public bool chkSession()
        {
            if (Session["IPQCUsr"] == null)
                return true;
            else
                return false;
        }

        public ActionResult RunReport()
        {


            return RedirectToAction("Index", "Report");
        }

   
        public ActionResult ViewPDF(string id)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            ViewBag.Title = id;
            ViewBag.Message = "IPQC Review Meeting Record";
            var ipqc = db.IPQCHeads.Find(id);

            // Type Rank
            ViewBag.Rank = db.TM_Rank;

            // Engineer DropDownList
            ViewBag.Engineer = from e in tnc.V_Employee_Info
                               where e.plant_id == 10 && e.emp_position == 1 && e.emp_status == "A"
                               orderby e.group_name
                               select e;
  
            var QCLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 4).FirstOrDefault();

            var ProductionLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 11).FirstOrDefault();

            var PELogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 12).FirstOrDefault();

            var PPLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 13).FirstOrDefault();

            var PLNLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 14).FirstOrDefault();

            var EngLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 15).FirstOrDefault();

            var EngMgrLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 9).FirstOrDefault();

            if (QCLogs != null)
            {
                ViewBag.QCName = (from e in tnc.V_Employee_Info
                                  where (QCLogs.EntryBy == e.emp_code)
                                  select e).FirstOrDefault();
                var date = QCLogs.EntryDate.ToString().Split();
                ViewBag.QCEntryDate = date[0];
            }

            if (ProductionLogs != null)
            {
                ViewBag.ProductionName = (from e in tnc.V_Employee_Info
                                          where (ProductionLogs.EntryBy == e.emp_code)
                                          select e).FirstOrDefault();
                var date = ProductionLogs.EntryDate.ToString().Split();
                ViewBag.ProductionEntryDate = date[0];
            }

            if (PELogs != null)
            {
                ViewBag.PEName = (from e in tnc.V_Employee_Info
                                  where (PELogs.EntryBy == e.emp_code)
                                  select e).FirstOrDefault();
                var date = PELogs.EntryDate.ToString().Split();
                ViewBag.PEEntryDate = date[0];
            }

            if (PPLogs != null)
            {
                ViewBag.PPName = (from e in tnc.V_Employee_Info
                                  where (PPLogs.EntryBy == e.emp_code)
                                  select e).FirstOrDefault();
                var date = PPLogs.EntryDate.ToString().Split();
                ViewBag.PPEntryDate = date[0];
            }

            if (PLNLogs != null)
            {
                ViewBag.PLNName = (from e in tnc.V_Employee_Info
                                   where (PLNLogs.EntryBy == e.emp_code)
                                   select e).FirstOrDefault();
                var date = PLNLogs.EntryDate.ToString().Split();
                ViewBag.PLNEntryDate = date[0];
            }

            if (EngMgrLogs != null)
            {
                ViewBag.EngMgrName = (from e in tnc.V_Employee_Info
                                      where (EngMgrLogs.EntryBy == e.emp_code)
                                      select e).FirstOrDefault();
                var date = EngMgrLogs.EntryDate.ToString().Split();
                ViewBag.EngMgrEntryDate = date[0];
            }

            ViewBag.QCLogs = QCLogs;

            ViewBag.ProductionLogs = ProductionLogs;

            ViewBag.PELogs = PELogs;

            ViewBag.PPLogs = PPLogs;

            ViewBag.PLNLogs = PLNLogs;

            ViewBag.EngLogs = EngLogs;

            ViewBag.EngMgrLogs = EngMgrLogs;

            var datetime = ipqc.EntryDate;
            var datetime_split = datetime.ToString().Split('/');
            List<string> months = new List<string>() { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            ViewBag.Date = (datetime_split[0] + " " + months[int.Parse(datetime_split[1]) - 1] + " " + datetime_split[2].Substring(2, 2));

            return new ViewAsPdf(ipqc)
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                PageMargins = new Margins(15, 0, 22, 0),
                PageWidth = 210,
                PageHeight = 297,
            };
            

        }

    }
}
