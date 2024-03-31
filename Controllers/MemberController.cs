using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using IPQC.Models;
using System.Security;
using System.IO;
using System.Diagnostics;


namespace IPQC.Controllers
{
    public class MemberController : Controller
    {
        TNC_ADMINEntities admin = new TNC_ADMINEntities();
        IPQC.Models.Util util = new IPQC.Models.Util();

        //
        // GET: /Member/

        public ActionResult Index()
        {
            return View();
        }

        //
        // GET: /Member/Login
        
        public ActionResult Login()
        {
            ViewBag.Title = "IPQC Login";
            return View();
        }

        //
        // GET: /Member/EasyPass
        //public ActionResult EasyPass(string id)
        //{
        //    var emp = admin.V_Employee_Info.Where(w => w.username == id && w.emp_status == "A").FirstOrDefault();

        //    if (emp != null)
        //    {
        //        if (emp.plant_id == 10)
        //            Session["IPQCUsrRole"] = "Engineer";
        //        else if (emp.dept_id == 37)
        //            Session["IPQCUsrRole"] = "PP";
        //        else if (emp.plant_id == 11)
        //            Session["IPQCUsrRole"] = "QC";
        //        else
        //            return RedirectToAction("Login", "Member");

        //        Session["IPQCUsr"] = emp.emp_code;
        //        Session["IPQCUsrInfo"] = emp.emp_fname + " " + emp.emp_lname + " (" + emp.LeafOrganize + ")";
        //        Session["IPQCUserLevel"] = (emp.emp_position == 2 || emp.emp_position == 4 || emp.emp_position == 6) ? "Mgr" : "User";
        //        Session["UserGroupId"] = emp.group_id;
        //        return RedirectToAction("Index", "Home");
        //    }
        //    else
        //        return RedirectToAction("Login", "Member");
        //}

        //
        // POST: /Member/Login

        [HttpPost]
        public ActionResult Login(string username, string password)
        {
            var encodedPassword = util.CalculateMD5Hash(password);
            var emp = admin.V_Employee_Info.Where(w => w.username == username /*&& w.password == encodedPassword*/ && w.emp_status == "A").FirstOrDefault();

            if (emp != null)
            {
                //Session["IPQCUsrRole"] คือหน่วยงานของ User
                if (emp.plant_id == 10)
                    Session["IPQCUsrRole"] = "Engineer";
                else if (emp.dept_id == 37)
                    Session["IPQCUsrRole"] = "PP";
                else if (emp.plant_id == 11)
                    Session["IPQCUsrRole"] = "QC";
                else if (emp.dept_id == 44)
                    Session["IPQCUsrRole"] = "PLN";
                else if (emp.plant_id == 2 || emp.plant_id == 3 || emp.plant_id == 4 || emp.plant_id == 6 || emp.plant_id == 17 || emp.plant_id == 18 || emp.plant_id == 33 || emp.plant_id == 34 || emp.plant_id == 36)
                    Session["IPQCUsrRole"] = "Production";
                else
                    Session["IPQCUsrRole"] = "Other";


                // all position_level 3 and (qc position_level 2) are user
                //if (emp.position_level == 3 || (emp.plant_id == 11 && emp.position_level == 2))
                //{
                //    Session["IPQCUserLevel"] = "User";
                //}

                //else if (emp.position_level == 4)
                //{
                //    Session["IPQCUserLevel"] = "Sup";
                //}

                //else if (emp.position_level == 5)
                //{
                //    Session["IPQCUserLevel"] = "Mgr";
                //}

                //else if (emp.position_level == 6)
                //{
                //    Session["IPQCUserLevel"] = "Dept";
                //}

                //else if (emp.position_level == 7)
                //{
                //    Session["IPQCUserLevel"] = "Plant";
                //}
                //else
                //{
                //    Session["IPQCUserLevel"] = "Other";
                //}

                Session["IPQCUserLevel"] = emp.position_level;

                Session["IPQCUsr"] = emp.emp_code;
                Session["IPQCUsrInfo"] = emp.emp_fname + " " + emp.emp_lname + " (" + emp.LeafOrgGroup + ")";

                Session["UserGroupId"] = (emp.group_id == null ? 0: emp.group_id);

                Session["UserDeptId"] = (emp.dept_id == null ? 0 : emp.dept_id);

                Session["UserPlantId"] = (emp.plant_id == null ? 0 : emp.plant_id);

                return Json(emp);
            }
            else
                return Json(string.Empty);
        }

        public ActionResult LogOff()
        {
            Session.Remove("IPQCUsr");
            Session.Remove("IPQCUsrInfo");
            Session.Remove("IPQCUsrRole");
            Session.Remove("IPQCUserLevel");
            Session.Remove("UserGroupId");
            Session.Remove("UserDeptId");
            Session.Remove("UserPlantId");

            return RedirectToAction("Login", "Member");
        }
    }
}
