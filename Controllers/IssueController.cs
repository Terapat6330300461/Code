using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using IPQC.Models;
using System.Linq.Dynamic;
using CenterUtil;
using System.IO;
using WebCommonFunction;

namespace IPQC.Controllers
{
    public class IssueController : Controller
    {
        IPQCEntities db = new IPQCEntities();
        TNC_ADMINEntities tnc = new TNC_ADMINEntities();
        qimnicsEntities qim = new qimnicsEntities();
        ClassUtil cUtil = new ClassUtil();
        TNCConversion conv = new TNCConversion();
        TNCFileDirectory dir = new TNCFileDirectory();
        TNCSecurity sec = new TNCSecurity();
        private TNCRunNumber run = new TNCRunNumber();

        public ActionResult Index()
        {
            return View();
        }

        //ADD BY N'ANZ
        [HttpPost]
        public JsonResult GetPCR(string term)
        {
             var query = (from p in db.View_PCR_no
                where p.pcr_id.Contains(term)
                select new
                {
                    id = p.pcr_id,
                    text = p.pcr_id,
                    order_id = p.id,
                }).Distinct().OrderBy(o => o.id.StartsWith(term) ? 0 : 1).ThenBy(o => o.order_id).Take(10);

            return Json(query, JsonRequestBehavior.AllowGet);
        }

        //ADD BY N'ANZ
        [HttpPost]
        public JsonResult GetNewItem(string term)
        {
            var query = (from p in db.View_NewItem_no
                         where p.wr_number.Contains(term)
                         select new
                         {
                             id = p.wr_number,
                             text = p.wr_number,
                         }).Distinct().OrderBy(o => o.id.StartsWith(term) ? 0 : 1).ThenBy(o => o.id).Take(10);

            return Json(query, JsonRequestBehavior.AllowGet);
        }

        //ADD BY N'ANZ
        [HttpPost]
        public JsonResult GetItemCode(string term)
        {
            var query = (from i in db.View_Itemcode
                         where i.eng_code.Contains(term)
                         select new
                         {
                             id = i.eng_code,
                             text = i.eng_code,
                         }).Distinct().OrderBy(o => o.id.StartsWith(term) ? 0 : 1).ThenBy(o => o.id).Take(10);

            return Json(query, JsonRequestBehavior.AllowGet);
        }

        //ADD BY N'ANZ
        [HttpPost]
        public JsonResult GetCustomer(string term)
        {
            var query = (from c in db.View_Customer
                         where c.customername.Contains(term)
                         select new
                         {
                             id = c.customername,
                             text = c.customername,
                         }).Distinct().OrderBy(o => o.id.StartsWith(term) ? 0 : 1).ThenBy(o => o.id).Take(10);

            return Json(query, JsonRequestBehavior.AllowGet);
        }
    }
}
