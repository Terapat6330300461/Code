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
    public class HomeController : Controller
    {
        IPQCEntities db = new IPQCEntities();
        TNC_ADMINEntities tnc = new TNC_ADMINEntities();
        qimnicsEntities qim = new qimnicsEntities();
        ClassUtil cUtil = new ClassUtil();
        TNCConversion conv = new TNCConversion();
        TNCFileDirectory dir = new TNCFileDirectory();
        TNCSecurity sec = new TNCSecurity();
        private TNCRunNumber run = new TNCRunNumber();

        ~HomeController()
        {
            db.Dispose();
            tnc.Dispose();
            qim.Dispose();
        }

        public bool chkSession()
        {
            if (Session["IPQCUsr"] == null)
                return true;
            else
                return false;
        }

        private string getEmail(string EmpId)
        {
            var emp = (from u in tnc.V_Employee_Info
                        where u.emp_code == EmpId
                        select u).FirstOrDefault();

            if (emp == null || emp.email == null)
            {
                return "";
            }

            return emp.email;
        }

        public ActionResult Index()
        {
            if (!string.IsNullOrEmpty(Request.QueryString["key"]))
            {
                var key = Request.QueryString["key"];
                var decode = sec.WebCenterDecode(key);
                var emp = sec.Login(decode, "a", false);
                if (emp != null)
                {
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

                    Session["IPQCUserLevel"] = emp.position_level;

                    Session["IPQCUsr"] = emp.emp_code;
                    Session["IPQCUsrInfo"] = emp.emp_fname + " " + emp.emp_lname + " (" + emp.LeafOrgGroup + ")";

                    Session["UserGroupId"] = (emp.group_id == null ? 0 : emp.group_id);

                    Session["UserDeptId"] = (emp.dept_id == null ? 0 : emp.dept_id);

                    Session["UserPlantId"] = (emp.plant_id == null ? 0 : emp.plant_id);
                }
            }

            if (chkSession()) return RedirectToAction("Login", "Member");

            ViewBag.Title = "Home Page";
            ViewBag.Message = "Search IPQC";
            // User Role
            ViewBag.UsrRole = Session["IPQCUsrRole"].ToString();
            // Type DropDownList
            ViewBag.Type = db.Types;
            // Status DropDownList
            ViewBag.Status = db.Status.OrderBy(o => o.Id >= 5 && o.Id <= 9) //status 5,8-9
                .ThenBy(o => o.Id > 10)//status 11-15
                .ThenBy(o => o.Id > 0 && o.Id < 5)//status 2-4
                .ThenBy(o => o.Id == 10)//status 10
                .ThenBy(o => o.Id == 0);//status 0
            // Issuer DropDownList
            var ihead = from i in db.IPQCHeads
                        group i by i.IssueBy into g
                        select g.Key;
            ViewBag.Issuer = (from i in ihead.ToList()
                              join u in tnc.V_Employee_Info.ToList()
                              on i equals u.emp_code
                              select u).OrderBy(o => o.group_name).ThenBy(o => o.emp_fname);
            // PP DropDownList
            var PPhead = from i in db.IPQCHeads
                         group i by i.PP into g
                         select g.Key;
            ViewBag.PP = (from i in PPhead.ToList()
                          join u in tnc.V_Employee_Info.Where(w => w.position_level <= 3).ToList()
                          on i equals u.emp_code
                          select u).OrderBy(o => o.group_name).ThenBy(o => o.emp_fname);

            // QC Dropdown
            ViewBag.QCGroup = tnc.View_Organization.Where(w => w.plant_id == 11 && w.group_id != 0 && w.active == true)
                .GroupBy(g => new { g.group_id, g.group_name })
                .Select(s => s.FirstOrDefault())
                .OrderBy(o => o.group_name)
                .ToList();
            // Eng Dropdown
            ViewBag.EngGroup = tnc.View_Organization.Where(w => w.plant_id == 10 && w.group_id != 0)
                .GroupBy(g => new { g.group_id, g.group_name })
                .Select(s => s.FirstOrDefault())
                .OrderBy(o => o.group_name)
                .ToList();
            ViewBag.Planning = tnc.View_Organization.Where(w => w.plant_id == 12 && w.dept_id == 44 && w.group_id != 0).OrderBy(o => o.group_name).Select(w => w.group_name).Distinct();

            //ADD by N'Anz
            // ProductionGroup Dropdown 
            ViewBag.ProductionGroup = db.P_SelectProductionGroup()
                .GroupBy(g => new { g.group_id, g.group_name })
                .Select(s => s.FirstOrDefault())
                .OrderBy(o => o.group_name)
                .ToList();


            return View();
        }

        public ActionResult GetIPQC(int page, int rows, string sidx, 
            string sord, string IPQCNo, string itemCode, byte? TypeId, 
            byte? StatusId, string Issuer, string JobNo, string PPDDL, 
            string QCDDL, string ProductionDDL, string ENGDDL)
        {
            if (chkSession()) return Json(0);

            var data = db.IPQCHeads.OrderBy(sidx + " " + sord);


            // Filter by search condition
            if (!string.IsNullOrEmpty(IPQCNo))
                data = data.Where(d => d.IPQCNo == IPQCNo);

            if (!string.IsNullOrEmpty(itemCode))
                data = data.Where(d => d.ItemCode.Contains(itemCode));

            if (TypeId != null)
                data = data.Where(d => d.TypeId == TypeId);

            if (StatusId != null)
                data = data.Where(d => d.StatusId == StatusId);

            if (!string.IsNullOrEmpty(Issuer))
                data = data.Where(d => d.IssueBy == Issuer);

            if (!string.IsNullOrEmpty(PPDDL))
                data = data.Where(d => d.PP == PPDDL);

            if (!string.IsNullOrEmpty(QCDDL))
            {
                var QCGroup_Id = int.Parse(QCDDL);
                data = data.Where(d => d.QCGroup == QCGroup_Id);
            }

            if (!string.IsNullOrEmpty(ProductionDDL))
            {
                var ProductionGroup_Id = int.Parse(ProductionDDL);
                data = data.Where(d => d.production_groupid == ProductionGroup_Id);
            }



            if (!string.IsNullOrEmpty(ENGDDL))
            {
                var ENGGroup_Id = int.Parse(ENGDDL);
                var Issuer_group = (from u in tnc.V_Employee_Info
                                    where u.group_id == ENGGroup_Id
                                    select u.emp_code).ToList();
                data = data.Where(d => Issuer_group.Contains(d.IssueBy));

                data.Count();
            }

            

            if (!string.IsNullOrEmpty(JobNo))
            {
                var job = JobNo.Split('-')[0];
                data = data.Where(d => d.IPQCLots.FirstOrDefault().JobNo.Contains(job));
            }

            // Prepare data
            var modUrl = Url.Action("Edit");
            var modUrl2 = Url.Action("Information");
            var modUrl3 = Url.Action("Judgement");
            var usrRole = Session["IPQCUsrRole"].ToString();
            var currentUser = Session["IPQCUsr"].ToString();
            var usrLevel = int.Parse(Session["IPQCUserLevel"].ToString());
            var usrGroupId = int.Parse(Session["UserGroupId"].ToString());
            var usrDeptId = int.Parse(Session["UserDeptId"].ToString());
            var usrPlantId = int.Parse(Session["UserPlantId"].ToString());
            double count = data.Count();

            var selectData = (from d in data.Skip((page - 1) * rows).Take(rows).ToList()
                              select d).Select(d => new
                              {
                                  id = d.IPQCNo,
                                  cell = new object[]
                                            {
                                                "<b><a style='color:#006090' class='information' href='" + modUrl2 + "?id=" + d.IPQCNo.Trim() + "'>"+d.IPQCNo+"</a></b>",
                                                db.IPQCProcesses.Where(w => w.IPQCNo == d.IPQCNo && w.ProcessId == 9).Count() + "/" + d.LotQty,
                                                d.Purpose,
                                                d.ItemCode,
                                                d.Type.Detail,
                                                d.EntryDate,
                                                d.Status.Detail,
                                                tnc.V_Employee_Info.Where(w => w.emp_code == d.IssueBy).Select(s => new { fullname = s.emp_fname + " " + s.emp_lname }).FirstOrDefault().fullname,
                                                tnc.V_Employee_Info.Where(w => w.emp_code == d.PP).Select(s => new { fullname = s.emp_fname + " " + s.emp_lname }).FirstOrDefault().fullname,
                                                tnc.View_Organization.Where(w => w.group_id == d.QCGroup).Select(s => s.group_name).FirstOrDefault(),
                                                //((((d.IssueBy == currentUser || d.Engineer == currentUser) && d.StatusId != 8 && d.StatusId != 9)|| //Engineer can Action(Close IPQC) Every Status except cancel, close status
                                                //((d.StatusId == 10 && ((d.EngMgr == currentUser)||
                                                //(tnc.V_Employee_Info.Find(d.IssueBy).dept_id == usrDeptId && usrLevel == "Dept" ) ||
                                                //(tnc.V_Employee_Info.Find(d.IssueBy).plant_id == usrPlantId && usrLevel == "Plant" ))))|| //Engineer Group, Dept, Plant Mgr can Action (Issue Approve)
                                                
                                                //(usrRole == "PP" && d.StatusId == 1 && d.PP == currentUser )|| //PP can Action (Confirm IPQC)
                                                
                                                //(usrRole == "QC" && d.StatusId == 3 && (usrLevel == "User" || usrLevel == "Sup") && usrGroupId == d.QCGroup) //QC can Action (Confirm IPQC)
                                                //?
                                                //"<b><a class='lnkDetails btn btn-success btn_table' style='margin-top:5px;'" +
                                                //" data-IPQCNo='" + d.IPQCNo.Trim() +
                                                //"' data-IPQCLotQty='" + d.LotQty +
                                                //"' data-PP='" + d.PP.Trim() + "'>Action</a></b>"
                                                //:
                                                // "<b><a class='lnkDetails btn btn_secondary btn_table' style='margin-top:5px;'" +
                                                //" data-IPQCNo='" + d.IPQCNo.Trim() +
                                                //"' data-IPQCLotQty='" + d.LotQty +
                                                //"' data-PP='" + d.PP.Trim() + "'>Detail</a></b>")) +

                                                "<b><a href='" + modUrl3 + "?id=" + d.IPQCNo.Trim() + "' class='btn btn_secondary btn_table' style='margin-top:5px;'>Preview</a></b>"     // Judgement Link
                                                
                                                + 

                                                (((d.StatusId == 10||d.StatusId == 1) &&
                                                (d.IssueBy == currentUser || d.Engineer == currentUser ||d.EngMgr == currentUser ||
                                                (tnc.V_Employee_Info.Find(d.IssueBy).group_id == usrGroupId && usrLevel == 4 ) ||
                                                (tnc.V_Employee_Info.Find(d.IssueBy).dept_id == usrDeptId && usrLevel == 6 ) ||
                                                (tnc.V_Employee_Info.Find(d.IssueBy).plant_id == usrPlantId && usrLevel == 7 )
                                                ))
                                                ? "<br/><b><a href='" + modUrl + "?id=" + d.IPQCNo.Trim() + "' class='btn btn-info btn_table'>Edit</a></b>" : "")    // Edit Link

                                          
                                            }
                              });

            object jsonData = new
            {
                page = page,
                total = Math.Ceiling(count / rows),
                records = count,
                rows = selectData
            };

            return Json(jsonData);
        }

        public ActionResult Create()
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            if (Session["IPQCUsrRole"].ToString() == "Engineer" && int.Parse(Session["IPQCUserLevel"].ToString()) <= 3)
            {

            }
            else
            {
                return RedirectToAction("Index", "Home");
            }


            ViewBag.Title = "Issue IPQC";
            ViewBag.Message = "Issue new IPQC";


            // Type DropDownList
            ViewBag.Type = db.Types;

            // Type DropDownList
            ViewBag.Rank = db.TM_Rank;

            // Engineer DropDownList
            ViewBag.Engineer = from e in tnc.V_Employee_Info
                               where e.plant_id == 10 && e.position_level <= 3 && e.emp_status == "A"
                               orderby e.group_name
                               select e;

            // PP DropDownList
            ViewBag.PP = from e in tnc.V_Employee_Info
                         where (e.dept_id == 37 && e.group_id != 0 && e.position_level <= 3 && e.emp_status == "A")
                         orderby e.group_name, e.emp_fname, e.emp_lname
                         select e;

            //EDIT by N'Anz
            // QC Dropdown
            ViewBag.QCGroup = tnc.View_Organization.Where(w => w.plant_id == 11 && w.group_id != 0 && w.active == true)
                .GroupBy(g => new { g.group_id, g.group_name })
                .Select(s => s.FirstOrDefault())
                .OrderBy(o => o.group_name)
                .ToList();

            //ADD by N'Anz
            // ProductionGroup Dropdown 
            ViewBag.ProductionGroup = db.P_SelectProductionGroup()
                .GroupBy(g => new { g.group_id, g.group_name })
                .Select(s => s.FirstOrDefault())
                .OrderBy(o => o.group_name)
                .ToList();

            //ADD by N'Anz
            // PCR Select2 
            //ViewBag.PCR = (from p in db.View_PCR_no orderby p.id select p).Take(10);

            // ADD NEW

            ViewBag.PLN = (from e in tnc.V_Employee_Info
                           where ((e.group_id == 14 || e.group_id == 95 || e.group_id == 105 || e.group_id == 148) && e.group_id != 0 && e.emp_status == "A")
                           select e)
                            .GroupBy(g => new { g.group_id, g.group_name })
                            .Select(s => s.FirstOrDefault())
                            .OrderBy(o => o.group_name)
                            .ToList();

            // ADD NEW

            ViewBag.PE = from e in tnc.V_Employee_Info
                         where (e.plant_id == 10 && e.group_id != 0 && e.emp_status == "A" && e.position_level <= 3)
                         orderby e.group_name, e.emp_fname, e.emp_lname
                         select e;

            return View();
        }

        // POST: /IssueIPQC/Create(IPQCHead model)
        [HttpPost]
        public ActionResult IssueIPQC(IPQCHead model, HttpPostedFileBase drawingfile)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");

            ViewBag.Title = "Issue";
            var runno = "";
            runno = run.GetRunNumber(125);

            IPQCHead ipqc = new IPQCHead();
            ipqc.IPQCNo = runno;
            ipqc.Purpose = model.Purpose;
            ipqc.ItemCode = model.ItemCode;
            ipqc.ReferenceNo = model.ReferenceNo;
            ipqc.NewItemNo = model.NewItemNo;
            ipqc.Customer = model.Customer;
            ipqc.PartNo = model.PartNo;
            ipqc.Drawingno = model.Drawingno;
            ipqc.DefectiveRate = model.DefectiveRate;
            ipqc.DefectiveTarget = model.DefectiveTarget;
            ipqc.Type_Size = model.Type_Size;
            ipqc.Material = model.Material;
            ipqc.Sampling = model.Sampling;
            ipqc.TypeId = model.TypeId;
            ipqc.RankId = model.RankId;
            ipqc.LotQty = (int)db.TM_Rank.Where(w => w.Id == ipqc.RankId).FirstOrDefault().LotQty;
            ipqc.Engineer = model.Engineer;
            ipqc.PP = model.PP;
            ipqc.QCGroup = model.QCGroup;
            ipqc.production_groupid = model.production_groupid;
            ipqc.PE = model.PE;
            var PEGroupId = tnc.V_Employee_Info.Where(w => w.emp_code == ipqc.PE).FirstOrDefault();
            if (PEGroupId != null)
            {
                ipqc.PE_Group_id = PEGroupId.group_id;
            }
            //ipqc.PLN_incharge = model.PLN_incharge;
            ipqc.PLN_groupid = model.PLN_groupid;
            ipqc.Note = model.Note;

            ipqc.StatusId = 10;
            ipqc.IssueBy = Session["IPQCUsr"].ToString();
            ipqc.EntryDate = DateTime.Now;
            ipqc.LastUpDate = DateTime.Now;

            // Get Engineer Mgr
            var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == ipqc.IssueBy).FirstOrDefault();
            var EngMgr = (from c in tnc.View_Organization
                          where c.group_id == eng_emp.group_id
                          select c.GroupMgr).FirstOrDefault();
            if (EngMgr != null)
            {
                ipqc.EngMgr = EngMgr;
            }
            else
            {
                EngMgr = (from c in tnc.View_Organization
                          where c.dept_id == eng_emp.dept_id
                          select c.DeptMgr).FirstOrDefault();
                if (EngMgr != null)
                {
                    ipqc.EngMgr = EngMgr;
                }
                else
                {
                    EngMgr = (from c in tnc.View_Organization
                              where c.plant_id == eng_emp.plant_id
                              select c.PlantMgr).FirstOrDefault();
                    if (EngMgr != null)
                    {
                        ipqc.EngMgr = EngMgr;
                    }
                }
            }



            var Start_PathFile = "Files/" + DateTime.Now.ToString("yyyy");

            if (drawingfile != null)
            {
                var targetPath = "";

                targetPath = Start_PathFile + "/" + runno + "/" + "ISSUE";


                var pathFile = drawingfile.FileName;
                var fileFullPath = pathFile.Split('\\');
                var filename = fileFullPath[fileFullPath.Length - 1];
                if (!Directory.Exists(Server.MapPath("~/" + targetPath)))
                {
                    Directory.CreateDirectory(Server.MapPath("~/" + targetPath));
                }
                var savedPath = dir.SaveFile(drawingfile, targetPath, filename);


                ipqc.Inspectionspec_filename = filename;
                ipqc.Inspectionspec_pathname = savedPath;

            }


            db.IPQCHeads.Add(ipqc);

            // IPQC Log
            IPQCLog log = new IPQCLog();
            log.IPQCNo = runno;
            log.StatusId = 0;
            log.EntryDate = DateTime.Now;
            log.EntryBy = Session["IPQCUsr"].ToString();

            db.IPQCLogs.Add(log);

            //// Mail Section
            //var emp = tnc.V_Employee_Info.Where(w => w.emp_code == ipqc.EngMgr).FirstOrDefault();
            List<string> ccList = new List<string>();
            var ccmail = "";
            var mgr_eng_email = "";
            if (EngMgr != null)
            {
                mgr_eng_email = getEmail(EngMgr);//mail to mgr. eng
            }


            var eng_email = getEmail(ipqc.IssueBy);//cc eng in-charge
            ccList.Add(eng_email);

            var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();

            var sup_eng_email = "";

            if (sup_eng != null)
            {
                foreach (var sub_emp in sup_eng)
                {
                    sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                    if (sup_eng_email != "")
                    {
                        ccList.Add(sup_eng_email);
                    }


                }
            }
            ccmail = string.Join(",", ccList);

            var title = "E-mail IPQC (IPQC No. : " + runno + ")";
            var textMail = "";
            var body = "";
            textMail = "<b>You have the IPQC online of Item " + model.ItemCode + " waiting you review and approve.</b>";
            body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + model.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + runno + @"<br></dd>
                            <dd> Item. : " + model.ItemCode + @"<br></dd>
                            <dd> Sampling : " + model.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href='" + "http://localhost:58735" + @"'> Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

            //cUtil.sendEMail(mgr_eng_email, title, body, ccmail);

     
            db.SaveChanges();
            run.SetRunNumber(125);

            return RedirectToAction("Index");
        }

        // GET: /Home/Edit/IPQCNo
        public ActionResult Edit(string id)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");


            ViewBag.Title = id;
            ViewBag.Message = "Edit IPQC";

            var ipqc = db.IPQCHeads.Find(id);
            var emp = tnc.V_Employee_Info.Find(ipqc.IssueBy);

            if (((Session["IPQCUsr"].ToString() == ipqc.IssueBy || Session["IPQCUsr"].ToString() == ipqc.Engineer) && (ipqc.StatusId == 10 || ipqc.StatusId == 1))

                || ((Session["IPQCUsr"].ToString() == ipqc.EngMgr
                    || (emp.dept_id == int.Parse(Session["UserDeptId"].ToString()) && int.Parse(Session["IPQCUserLevel"].ToString()) == 6)
                    || (emp.plant_id == int.Parse(Session["UserPlantId"].ToString()) && int.Parse(Session["IPQCUserLevel"].ToString()) == 7))
                && (ipqc.StatusId == 10 || ipqc.StatusId == 1))

                || (emp.group_id == int.Parse(Session["UserGroupId"].ToString()) && int.Parse(Session["IPQCUserLevel"].ToString()) == 4))
            {

            }
            else
            {
                return RedirectToAction("Index", "Home");
            }



            // Type DropDownList
            ViewBag.Type = db.Types;

            // Type Rank
            ViewBag.Rank = db.TM_Rank;

            // Engineer DropDownList
            ViewBag.Engineer = from e in tnc.V_Employee_Info
                               where e.plant_id == 10 && e.position_level == 3 && e.emp_status == "A"
                               orderby e.group_name
                               select e;

            // PP DropDownList
            ViewBag.PP = from e in tnc.V_Employee_Info
                         where (e.dept_id == 37 && e.group_id != 0 && e.position_level <= 3 && e.emp_status == "A")
                         orderby e.group_name, e.emp_fname, e.emp_lname
                         select e;

            // QC Dropdown
            ViewBag.QCGroup = tnc.View_Organization.Where(w => w.plant_id == 11 && w.group_id != 0 && w.active == true)
                .GroupBy(g => new { g.group_id, g.group_name })
                .Select(s => s.FirstOrDefault())
                .OrderBy(o => o.group_name)
                .ToList();

            // ProductionGroup Dropdown
            ViewBag.ProductionGroup = db.P_SelectProductionGroup()
                .GroupBy(g => new { g.group_id, g.group_name })
                .Select(s => s.FirstOrDefault())
                .OrderBy(o => o.group_name)
                .ToList();

            // ADD NEW
            ViewBag.PLN = (from e in tnc.V_Employee_Info
                           where ((e.group_id == 14 || e.group_id == 95 || e.group_id == 105 || e.group_id == 148) && e.group_id != 0 && e.emp_status == "A")
                           select e)
                  .GroupBy(g => new { g.group_id, g.group_name })
                  .Select(s => s.FirstOrDefault())
                  .OrderBy(o => o.group_name)
                  .ToList();

            // ADD NEW

            ViewBag.PE = from e in tnc.V_Employee_Info
                         where (e.plant_id == 10 && e.group_id != 0 && e.emp_status == "A" && e.position_level <= 3)
                         orderby e.group_name, e.emp_fname, e.emp_lname
                         select e;

          

            return View(ipqc);
        }

        // POST: /Home/SaveIPQC(IPQCHead model)
        [HttpPost]
        public ActionResult SaveIPQC(IPQCHead model, HttpPostedFileBase drawingfile)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");

            ViewBag.Title = "Edit";

            var ipqc = db.IPQCHeads.Find(model.IPQCNo);
            ipqc.Purpose = model.Purpose;
            ipqc.ItemCode = model.ItemCode;
            ipqc.ReferenceNo = model.ReferenceNo;
            ipqc.NewItemNo = model.NewItemNo;
            ipqc.Customer = model.Customer;
            ipqc.PartNo = model.PartNo;
            ipqc.Drawingno = model.Drawingno;
            ipqc.DefectiveRate = model.DefectiveRate;
            ipqc.DefectiveTarget = model.DefectiveTarget;
            ipqc.Type_Size = model.Type_Size;
            ipqc.Material = model.Material;
            ipqc.Sampling = model.Sampling;
            ipqc.TypeId = model.TypeId;
            ipqc.RankId = model.RankId;
            ipqc.LotQty = (int)db.TM_Rank.Where(w => w.Id == ipqc.RankId).FirstOrDefault().LotQty;
            ipqc.Engineer = model.Engineer;
            ipqc.PP = model.PP;
            ipqc.QCGroup = model.QCGroup;
            ipqc.production_groupid = model.production_groupid;
            //ipqc.PLN_incharge = model.PLN_incharge;
            ipqc.PE = model.PE;
            var PEGroupId = tnc.V_Employee_Info.Where(w => w.emp_code == ipqc.PE).FirstOrDefault();
            if (PEGroupId != null)
            {
                ipqc.PE_Group_id = PEGroupId.group_id;
            }
            ipqc.PLN_groupid = model.PLN_groupid;
            ipqc.Note = model.Note;

            ipqc.EntryDate = DateTime.Now;
            ipqc.LastUpDate = DateTime.Now;

            // Get Engineer Mgr
            var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == ipqc.IssueBy).FirstOrDefault();
            var EngMgr = (from c in tnc.View_Organization
                          where c.group_id == eng_emp.group_id
                          select c.GroupMgr).FirstOrDefault();
            if (EngMgr != null)
            {
                ipqc.EngMgr = EngMgr;
            }
            else
            {
                EngMgr = (from c in tnc.View_Organization
                          where c.dept_id == eng_emp.dept_id
                          select c.DeptMgr).FirstOrDefault();
                if (EngMgr != null)
                {
                    ipqc.EngMgr = EngMgr;
                }
                else
                {
                    EngMgr = (from c in tnc.View_Organization
                              where c.plant_id == eng_emp.plant_id
                              select c.PlantMgr).FirstOrDefault();
                    if (EngMgr != null)
                    {
                        ipqc.EngMgr = EngMgr;
                    }
                }
            }

            var Start_PathFile = "Files/" + DateTime.Now.ToString("yyyy");

            if (drawingfile != null)
            {
                var targetPath = "";

                targetPath = Start_PathFile + "/" + model.IPQCNo + "/" + "ISSUE";
                var pathFile = drawingfile.FileName;
                var fileFullPath = pathFile.Split('\\');
                var filename = fileFullPath[fileFullPath.Length - 1];
                if (!Directory.Exists(Server.MapPath("~/" + targetPath)))
                {
                    Directory.CreateDirectory(Server.MapPath("~/" + targetPath));
                }
                else if (Directory.Exists(Server.MapPath("~/" + targetPath)))
                {
                    //clear all old file
                    DirectoryInfo di = new DirectoryInfo(Server.MapPath("~/" + targetPath));
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }
                }
                var savedPath = dir.SaveFile(drawingfile, targetPath, filename);


                ipqc.Inspectionspec_filename = filename;
                ipqc.Inspectionspec_pathname = savedPath;

            }

            if (ipqc.StatusId == 10 && int.Parse(Session["IPQCUserLevel"].ToString()) < 5)
            {
                //// Mail Section
                //var emp = tnc.V_Employee_Info.Where(w => w.emp_code == ipqc.EngMgr).FirstOrDefault();
                List<string> ccList = new List<string>();
                var ccmail = "";
                var mgr_eng_email = "";
                if (EngMgr != null)
                {
                    mgr_eng_email = getEmail(EngMgr);//mail to mgr. eng
                }

                var eng_email = getEmail(ipqc.IssueBy);//cc eng in-charge
                ccList.Add(eng_email);

                var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();

                var sup_eng_email = "";

                if (sup_eng != null)
                {
                    foreach (var sub_emp in sup_eng)
                    {
                        sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                        if (sup_eng_email != "")
                        {
                            ccList.Add(sup_eng_email);
                        }
                    }
                }
                ccmail = string.Join(",", ccList);

                var title = "E-mail IPQC (IPQC No. : " + ipqc.IPQCNo + ")";
                var textMail = "";
                var body = "";
                textMail = "<b>You have the IPQC online of Item " + model.ItemCode + " waiting you review and approve.</b>";
                body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + model.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + model.IPQCNo + @"<br></dd>
                            <dd> Item. : " + model.ItemCode + @"<br></dd>
                            <dd> Sampling : " + model.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href='" + "http://localhost:58735" + @"'> Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

                //cUtil.sendEMail(mgr_eng_email, title, body, ccmail);
            }

            db.SaveChanges();

            return RedirectToAction("Index", "Home");
        }

        // GET: /Home/GetLotQty
        public ActionResult GetLotQty(string IPQCNo)
        {
            if (chkSession()) return Json(-1);

            var lotQty = db.IPQCHeads.Where(i => i.IPQCNo == IPQCNo).Select(s => s.LotQty).FirstOrDefault();

            return Json(lotQty, JsonRequestBehavior.AllowGet);
        }

        // POST: /Home/SetLotQty
        [HttpPost]
        public ActionResult SetLotQty(string IPQCNo, int lot)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");

            var lotQty = db.IPQCHeads.Where(i => i.IPQCNo == IPQCNo).FirstOrDefault();
            lotQty.LotQty = lot;
            if (db.SaveChanges() > 0)
            {
                var obj = new
                {
                    IPQCNo = IPQCNo,
                    lotQty = lot
                };

                return Json(obj);
            }
            else
            {
                return Json(string.Empty);
            }
        }

        // GET: /Home/LotDetailsPartial 
        [OutputCache(Duration = 0)]
        public ActionResult LotDetailsPartial(string IPQCNo, Int16 Lot)
        {
            //if (chkSession()) return Json(0);

            var hdata = db.IPQCHeads.Where(i => i.IPQCNo == IPQCNo).FirstOrDefault();
            ViewBag.hdata = hdata;
            var emp = tnc.V_Employee_Info.Find(hdata.IssueBy);

            ViewBag.dept_id = emp.dept_id;
            ViewBag.plant_id = emp.plant_id;

            ViewBag.QCStaff = hdata.IPQCLogs.Where(w => w.StatusId == 3).FirstOrDefault();

            List<Log> lstLog = new List<Log>();
            Log objLog;
            var tmpLog = (from d in hdata.IPQCLogs.ToList()
                          join t in tnc.V_Employee_Info.ToList()
                          on d.EntryBy equals t.emp_code into j
                          from t in j.DefaultIfEmpty()
                          select new
                          {
                              logID = d.StatusId,
                              operation = d.Status.Detail,
                              opdate = d.EntryDate,
                              actor = t != null ? t.emp_fname + " " + t.emp_lname : d.EntryBy
                          }).OrderBy(o => o.opdate);


            foreach (var item in tmpLog)
            {
                objLog = new Log();
                if (item.operation.StartsWith("Waiting for "))
                {
                    objLog.operation = item.operation.Substring(12);
                }
                else
                {
                    objLog.operation = item.operation;
                }
                objLog.opdate = item.opdate;
                objLog.actor = item.actor;

                lstLog.Add(objLog);
            }
            ViewBag.log = lstLog;

            ViewBag.HStatusId = hdata.StatusId;
            ViewBag.PP = tnc.V_Employee_Info.Where(w => w.emp_code == hdata.PP).FirstOrDefault();
            var ldata = db.IPQCLots.Where(d => d.IPQCNo == IPQCNo && d.Lot == Lot);
            ViewBag.ldata = ldata.FirstOrDefault();
            ViewBag.lot = Lot;
            var firstLot = db.IPQCLots.Where(d => d.IPQCNo == IPQCNo && d.Lot == 1).FirstOrDefault();
            if (firstLot != null)
            {
                ViewBag.firstLot = firstLot;
            }

            if (db.IPQCProcesses.Where(p => p.IPQCNo == IPQCNo).FirstOrDefault() == null)
            {
                ViewBag.status = "N/A";
            }
            else { 

                var ss = (from d in ldata
                            select new
                            {
                                pid = d.IPQCProcesses.Max(m => m.ProcessId)
                            }).FirstOrDefault();
               
            

                ViewBag.statusId = (ss == null ? 0 : ss.pid);

                if (ss == null)
                {
                    ViewBag.status = "N/A";
                }
                else
                {
                    var status = db.Processes.Where(p => p.Id == ss.pid).Select(s => s.Detail).FirstOrDefault();
                    if (status == null)
                    {
                        ViewBag.status = "N/A";
                    }
                    else
                    {
                        ViewBag.status = status;
                    }
                }
            }

            return PartialView(JsonRequestBehavior.AllowGet);
        }

        //// GET: /Home/JobRoutePartial
        //public ActionResult JobRoutePartial(string IPQCNo, Int16 Lot)
        //{
        //    if (chkSession()) return Json(0, JsonRequestBehavior.AllowGet);

        //    var iLot = db.IPQCLots.Where(d => d.IPQCNo == IPQCNo && d.Lot == Lot).FirstOrDefault();
        //    if (iLot != null)
        //    {
        //        var jobNo = iLot.JobNo.Split('-').Length == 2 ? iLot.JobNo : (iLot.JobNo.Split('-')[0] + "-" + (iLot.Lot < 10 ? "0" + iLot.Lot.ToString() : iLot.Lot.ToString()) + "0");
        //        var jobRoutes = qim.tr_job_progress_nics.Where(r => r.job_order_no == jobNo);

        //        List<List<string>> lstRoute = new List<List<string>>();

        //        if (jobRoutes.Any())
        //        {
        //            var jobRoute = jobRoutes.FirstOrDefault();
        //            List<string> lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_00.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_00 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_01.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_01 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_02.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_02 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_03.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_03 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_04.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_04 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_05.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_05 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_06.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_06 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_07.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_07 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_08.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_08 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_09.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_09 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_10.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_10 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_11.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_11 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_12.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_12 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_13.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_13 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_14.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_14 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_15.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_15 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_16.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_16 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_17.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_17 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_18.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_18 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            ViewBag.route = lstRoute;
        //        }
        //    }

        //    return PartialView();
        //}

        //// GET: /Home/JobStatusPartial
        //public ActionResult JobStatusPartial(string IPQCNo, Int16 Lot)
        //{
        //    if (chkSession()) return RedirectToAction("Login", "Member");

        //    var iLot = db.IPQCLots.Where(d => d.IPQCNo == IPQCNo && d.Lot == Lot).FirstOrDefault();
        //    if (iLot != null)
        //    {
        //        var jobNo = iLot.JobNo.Split('-').Length == 2 ? iLot.JobNo : (iLot.JobNo.Split('-')[0] + "-" + (iLot.Lot < 10 ? "0" + iLot.Lot.ToString() : iLot.Lot.ToString()) + "0");
        //        var jobRoutes = qim.td_job_progress_nics.Where(r => r.job_order_no == jobNo);
        //        List<List<string>> lstRoute = new List<List<string>>();

        //        if (jobRoutes.Any())
        //        {
        //            var jobRoute = jobRoutes.FirstOrDefault();
        //            List<string> lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_00.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_00 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_01.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_01 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_02.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_02 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_03.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_03 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_04.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_04 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_05.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_05 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_06.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_06 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_07.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_07 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_08.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_08 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_09.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_09 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_10.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_10 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_11.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_11 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_12.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_12 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_13.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_13 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_14.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_14 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_15.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_15 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_16.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_16 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_17.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_17 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            lstInner = new List<string>();
        //            lstInner.Add(jobRoute.operation_code_18.Trim());
        //            lstInner.Add((from w in qim.tm_workcenter where w.wc == jobRoute.operation_code_18 select w.wc_name).FirstOrDefault());
        //            lstRoute.Add(lstInner);

        //            ViewBag.route = lstRoute;
        //        }
        //    }

        //    return PartialView();
        //}

        [HttpPost]
        public ActionResult EngApprove(string IPQCNo)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");

            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();

            IPQC.StatusId = 1;

            // IPQC Log
            IPQCLog log = new IPQCLog();
            log.IPQCNo = IPQCNo;
            log.StatusId = 10;
            log.EntryDate = DateTime.Now;
            log.EntryBy = Session["IPQCUsr"].ToString();

            db.IPQCLogs.Add(log);

            //// Mail Section
            List<string> ccList = new List<string>();
            var ccmail = "";
            var pp_email = getEmail(IPQC.PP);//mail to pp


            if (IPQC.EngMgr != null)
            {
                var mgr_eng_email = getEmail(IPQC.EngMgr);//cc mgr. eng
                ccList.Add(mgr_eng_email);

            }


            var eng_email = getEmail(IPQC.IssueBy);//cc eng in-charge
            ccList.Add(eng_email);

            var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
            var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
            var sup_eng_email = "";
            if (sup_eng != null)
            {
                foreach (var sub_emp in sup_eng)
                {
                    sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                    if (sup_eng_email != "")
                    {
                        ccList.Add(sup_eng_email);
                    }

                }
            }

            var PPGroupId = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.PP).FirstOrDefault();
            var sup_pp = tnc.V_Employee_Info.Where(w => w.group_id == PPGroupId.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
            var sup_pp_email = "";
            if (sup_pp != null)
            {
                foreach (var sub_emp in sup_pp)
                {
                    sup_pp_email = getEmail(sub_emp.emp_code);//cc sup. pp
                    if (sup_pp_email != "")
                    {
                        ccList.Add(sup_pp_email);
                    }

                }
            }
            var PPMgr = (from c in tnc.View_Organization
                         where c.group_id == PPGroupId.group_id
                         select c.GroupMgr).FirstOrDefault();
            if (PPMgr != null)
            {
                var mgr_pp_email = getEmail(PPMgr);//cc mgr. pp
                ccList.Add(mgr_pp_email);
            }

            ccmail = string.Join(",", ccList);

            var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
            var textMail = "";
            var body = "";
            textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " waiting for confirm IPQC</b>";
            body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href='" + "http://localhost:58735" + @"'> Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

            //cUtil.sendEMail(pp_email, title, body, ccmail);

            
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult PPConfirm(string IPQCNo)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");

            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();

            IPQC.StatusId = 3;

            // IPQC Log
            IPQCLog log = new IPQCLog();
            log.IPQCNo = IPQCNo;
            log.StatusId = 1;
            log.EntryDate = DateTime.Now;
            log.EntryBy = Session["IPQCUsr"].ToString();

            db.IPQCLogs.Add(log);


            for (var i = 1;i<=IPQC.LotQty;i++)
            {
                IPQCLot lot = new IPQCLot();
                lot.IPQCNo = IPQCNo;
                lot.Lot = i;
                lot.EntryDate = DateTime.Now;
                lot.Sampling = IPQC.Sampling;
                lot.Purpose = IPQC.Purpose;
                lot.ItemCode = IPQC.ItemCode;
                lot.Customer = IPQC.Customer;
                lot.transfersign = "";
                db.IPQCLots.Add(lot);

                IPQCProcess process = new IPQCProcess();
                process.IPQCNo = IPQCNo;
                process.Lot = i;
                process.ProcessId = 1;
                process.ProcessDate = DateTime.Now;
                process.ProcessBy = Session["IPQCUsr"].ToString();

                db.IPQCProcesses.Add(process);
            }


            //// Mail Section
            List<string> ccList = new List<string>();
            var ccmail = "";

            var qc_emps = tnc.V_Employee_Info.Where(w => w.group_id == IPQC.QCGroup && w.position_level < 4 && w.emp_status == "A");
            List<string> qc_emp_list = new List<string>();

            string mail_to = "";
            var qc_emp_email = "";
            if (qc_emps != null)
            {
                foreach (var qc_emp in qc_emps)
                {
                    qc_emp_email = getEmail(qc_emp.emp_code);//cc sup. eng
                    if(qc_emp_email != "")
                    {
                        qc_emp_list.Add(qc_emp_email);
                    }
                    
                    
                }
            }
            mail_to = string.Join(",", qc_emp_list);



            if (IPQC.EngMgr != null)
            {
                var mgr_eng_email = getEmail(IPQC.EngMgr);//cc mgr. eng
                ccList.Add(mgr_eng_email);

            }


            var eng_email = getEmail(IPQC.Engineer);//cc eng in-charge
            ccList.Add(eng_email);

            var EngGroupId = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
            var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == EngGroupId.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
            var sup_eng_email = "";
            if (sup_eng != null)
            {
                foreach (var sub_emp in sup_eng)
                {
                    sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                    if (sup_eng_email != "")
                    {
                        ccList.Add(sup_eng_email);
                    }
                    
                }
            }

            var sup_qc = tnc.V_Employee_Info.Where(w => w.group_id == IPQC.QCGroup && w.emp_position == 5 && w.emp_status == "A").ToList();
            var sup_qc_email = "";
            if (sup_qc != null)
            {
                foreach (var sub_emp in sup_qc)
                {
                    sup_qc_email = getEmail(sub_emp.emp_code);//cc sup. qc
                    if (sup_qc_email != "")
                    {
                        ccList.Add(sup_qc_email);

                    }
                }
            }
            var QCMgr = (from c in tnc.View_Organization
                         where c.group_id == IPQC.QCGroup
                         select c.GroupMgr).FirstOrDefault();
            if (QCMgr != null)
            {
                var mgr_qc_email = getEmail(QCMgr);//cc mgr. qc
                ccList.Add(mgr_qc_email);
            }

            ccmail = string.Join(",", ccList);
            var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
            var textMail = "";
            var body = "";
            textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " waiting for process IPQC (LOT : "+ db.IPQCProcesses.Where(w => w.IPQCNo == IPQC.IPQCNo && w.ProcessId == 9).Count() + @"/"+IPQC.LotQty + @")</b>";
            body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href='" + "http://localhost:58735" + @"'> Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

            //cUtil.sendEMail(mail_to, title, body, ccmail);

            db.SaveChanges();

            return RedirectToAction("Index");
        }



        // POST: /Home/InsertLot
        public ActionResult InsertLot(string IPQCNo, int IPQCLot, string startDate, string expireDate, string jobNo, string tagNo)
        {
            if (chkSession()) return Json(-1);

            var start = db.IPQCLots.Where(l => l.IPQCNo == IPQCNo && l.Lot == 1).FirstOrDefault();
            var currentLot = db.IPQCLots.Where(l => l.IPQCNo == IPQCNo && l.Lot == IPQCLot).FirstOrDefault();

            // Insert IPQCLot
            if (currentLot == null)
            {
                IPQCLot lot = new IPQCLot();

                if (start == null && (string.IsNullOrEmpty(startDate)))
                {
                    return Json(string.Empty);
                }

                if (IPQCLot == 1)
                {
                    lot.StartDate = conv.DateDisplayToDB(startDate);
                    lot.ExpireDate = conv.DateDisplayToDB(expireDate);
                }
                else if (IPQCLot > 1 && start != null)
                {
                    lot.StartDate = start.StartDate;
                    lot.ExpireDate = start.ExpireDate;
                }
                else
                {
                    return Json(string.Empty);
                }

                lot.IPQCNo = IPQCNo;
                lot.JobNo = jobNo;
                lot.Lot = IPQCLot;
                lot.TagNo = tagNo;

                lot.EntryDate = DateTime.Now;
                lot.LastUpDate = DateTime.Now;

                db.IPQCLots.Add(lot);

                // Insert IPQC Process
                IPQCProcess iprocess = new IPQCProcess();
                iprocess.IPQCNo = IPQCNo;
                iprocess.Lot = IPQCLot;
                iprocess.ProcessId = 1;
                iprocess.ProcessDate = DateTime.Now;
                iprocess.ProcessBy = Session["IPQCUsr"].ToString();

                db.IPQCProcesses.Add(iprocess);
                ViewBag.jobNo = lot.JobNo;

                // Save to DB
                db.SaveChanges();

                var iLotCount = db.IPQCLots.Where(w => w.IPQCNo == IPQCNo).Count();
                var iHead = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();

                // If update all Job Lot then update head status and insert Log
                var retVal = 1;
                if (iLotCount == iHead.LotQty)
                {
                    iHead.StatusId = 3;

                    // IPQC Log
                    IPQCLog log = new IPQCLog();
                    log.IPQCNo = IPQCNo;
                    log.StatusId = 1;
                    log.EntryDate = DateTime.Now;
                    log.EntryBy = Session["IPQCUsr"].ToString();

                    db.IPQCLogs.Add(log);

                    db.SaveChanges();
                    retVal = 3;
                }

                return Json(retVal);
            }
            // Update IPQCLot
            else
            {
                var allLots = db.IPQCLots.Where(l => l.IPQCNo == IPQCNo);

                if (start == null && string.IsNullOrEmpty(startDate))
                {
                    return Json(string.Empty);
                }
                else if (IPQCLot == 1)
                {
                    currentLot.StartDate = conv.DateDisplayToDB(startDate);
                    currentLot.ExpireDate = conv.DateDisplayToDB(expireDate);

                    // Update startDate, expireDate for all Lots
                    foreach (var item in allLots)
                    {
                        item.StartDate = currentLot.StartDate;
                        item.ExpireDate = currentLot.ExpireDate;
                    }
                }


                currentLot.JobNo = jobNo;
                currentLot.TagNo = tagNo;

                currentLot.LastUpDate = DateTime.Now;
                ViewBag.jobNo = currentLot.JobNo;

                // Save to DB
                db.SaveChanges();

                return Json(2);
            }
        }

        public ActionResult CancelLot(string IPQCNo, int IPQCLot)
        {
            if (chkSession())
            {
                return Json(-1);
            }
            else
            {
                var iPrcoess_cancel = db.IPQCProcesses.Where(w => w.IPQCNo == IPQCNo && w.Lot == IPQCLot && w.ProcessId == 8).FirstOrDefault();
                var iDtl = db.IPQCLots.Where(w => w.IPQCNo == IPQCNo && w.Lot == IPQCLot).FirstOrDefault();
                if (iPrcoess_cancel == null)
                {
                    if ((IPQCLot == 1 && iDtl != null) || IPQCLot != 1)
                    {
                        if (iDtl == null)
                        {
                            IPQCLot iLot = new IPQCLot();
                            iLot.IPQCNo = IPQCNo;
                            iLot.Lot = IPQCLot;
                            db.IPQCLots.Add(iLot);
                        }

                        IPQCProcess iProcess = new IPQCProcess();
                        iProcess.IPQCNo = IPQCNo;
                        iProcess.Lot = IPQCLot;
                        iProcess.ProcessId = 8;
                        iProcess.ProcessDate = DateTime.Now;
                        iProcess.ProcessBy = Session["IPQCUsr"].ToString();
                        db.IPQCProcesses.Add(iProcess);


                        db.SaveChanges();
                        return Json(1);

                    }

                }
                return Json(0);
            }
        }

        public ActionResult AttachSummaryResult()
        {
            var dimension_file = Request.Files["SRFile"];
            var defective_file = Request.Files["SRFile2"];
            var qc_target = Request.Form["qcTarget"];
            var qc_actual = Request.Form["qcActual"];
            var IPQCNo = Request.Form["hddIPQCNo"];

            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();

            var Start_PathFile = "Files/" + DateTime.Now.ToString("yyyy");

            if (dimension_file != null)
            {
                var dmTargetPath = "";

                dmTargetPath = Start_PathFile + "/" + IPQCNo + "/" + "QC_Dimension_Result";


                var dmPathFile = dimension_file.FileName;
                var dmFileFullPath = dmPathFile.Split('\\');
                var dmFilename = dmFileFullPath[dmFileFullPath.Length - 1];

                if (!Directory.Exists(Server.MapPath("~/" + dmTargetPath)))
                {
                    Directory.CreateDirectory(Server.MapPath("~/" + dmTargetPath));
                }
                else if (Directory.Exists(Server.MapPath("~/" + dmTargetPath)))
                {
                    //clear all old file
                    DirectoryInfo di = new DirectoryInfo(Server.MapPath("~/" + dmTargetPath));
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }
                }
                var dmSavedPath = dir.SaveFile(dimension_file, dmTargetPath, dmFilename);

                IPQC.QCResultFile = dmSavedPath;

            }


            if (defective_file != null)
            {
                var dfTargetPath = "";

                dfTargetPath = Start_PathFile + "/" + IPQCNo + "/" + "QC_Defective_Result";


                var dfPathFile = defective_file.FileName;
                var dfFileFullPath = dfPathFile.Split('\\');
                var dfFilename = dfFileFullPath[dfFileFullPath.Length - 1];

                if (!Directory.Exists(Server.MapPath("~/" + dfTargetPath)))
                {
                    Directory.CreateDirectory(Server.MapPath("~/" + dfTargetPath));
                }
                else if (Directory.Exists(Server.MapPath("~/" + dfTargetPath)))
                {
                    //clear all old file
                    DirectoryInfo di = new DirectoryInfo(Server.MapPath("~/" + dfTargetPath));
                    foreach (FileInfo file in di.GetFiles())
                    {
                        file.Delete();
                    }
                }
                var dfSavedPath = dir.SaveFile(defective_file, dfTargetPath, dfFilename);

                IPQC.QCDefectiveResultFile = dfSavedPath;

            }

            if (IPQC != null)
            {
                IPQC.QCDefectiveRate_Target = qc_target;
                IPQC.QCDefectiveRate_Actual = qc_actual;

                IPQC.StatusId = 4;

                // IPQC Log
                IPQCLog log = new IPQCLog();
                log.IPQCNo = IPQCNo;
                log.StatusId = 3;
                log.EntryDate = DateTime.Now;
                log.EntryBy = Session["IPQCUsr"].ToString();

                db.IPQCLogs.Add(log);
            }

            //// Mail Section

            List<string> ccList = new List<string>();
            var ccmail = "";
            var mgr_eng_email = "";
            var qc_mgr_email = "";
            var qc_emp = tnc.V_Employee_Info.Where(w => w.group_id == IPQC.QCGroup).FirstOrDefault();
            var QCMgr = (from c in tnc.View_Organization
                         where c.group_id == qc_emp.group_id
                         select c.GroupMgr).FirstOrDefault();
            if (QCMgr != null)
            {
                qc_mgr_email = getEmail(QCMgr);//mail to mgr. group qc
            }
            else
            {
                QCMgr = (from c in tnc.View_Organization
                          where c.dept_id == qc_emp.dept_id
                          select c.DeptMgr).FirstOrDefault();
                if (QCMgr != null)
                {
                    qc_mgr_email = getEmail(QCMgr);//mail to mgr. dept qc
                }
                else
                {
                    QCMgr = (from c in tnc.View_Organization
                              where c.plant_id == qc_emp.plant_id
                              select c.PlantMgr).FirstOrDefault();
                    if (QCMgr != null)
                    {
                        qc_mgr_email = getEmail(QCMgr);//mail to mgr. plant qc

                    }
                }
            }


            if (IPQC.EngMgr != null)
            {
                mgr_eng_email = getEmail(IPQC.EngMgr);//cc mgr. eng
                ccList.Add(mgr_eng_email);

            }


            var eng_email = getEmail(IPQC.IssueBy);//cc eng in-charge
            ccList.Add(eng_email);

            var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
            var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
            var sup_eng_email = "";
            if (sup_eng != null)
            {
                foreach (var sub_emp in sup_eng)
                {
                    sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                    if (sup_eng_email != "")
                    {
                        ccList.Add(sup_eng_email);
                    }

                }
            }

            ccmail = string.Join(",", ccList);

            var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
            var textMail = "";
            var body = "";
            textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " waiting for Comment & sign</b>";
            body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href = '" + "http://localhost:58735/Home/Information?id=" + IPQC.IPQCNo + @"' > Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

            //cUtil.sendEMail(qc_mgr_email, title, body, ccmail);

            db.SaveChanges();




            return RedirectToAction("Index", "Home");
        }

        public ActionResult QCJudge(string IPQCNo, string QCComment)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();
            if (IPQC != null)
            {
                IPQC.StatusId = 11;

                //IPQC Log
                IPQCLog log = new IPQCLog();
                log.IPQCNo = IPQCNo;
                log.StatusId = 4;
                log.EntryDate = DateTime.Now;
                log.EntryBy = Session["IPQCUsr"].ToString();
                log.Comment = QCComment;

                db.IPQCLogs.Add(log);

                //// Mail Section

                List<string> ccList = new List<string>();
                var ccmail = "";
                var mgr_eng_email = "";
                var production_mgr_email = "";
                var production_emp = tnc.V_Employee_Info.Where(w => w.group_id == IPQC.production_groupid).FirstOrDefault();
                var ProductionMgr = (from c in tnc.View_Organization
                             where c.group_id == production_emp.group_id
                             select c.GroupMgr).FirstOrDefault();
                if (ProductionMgr != null)
                {
                    production_mgr_email = getEmail(ProductionMgr);//mail to mgr. group qc
                }
                else
                {
                    ProductionMgr = (from c in tnc.View_Organization
                             where c.dept_id == production_emp.dept_id
                             select c.DeptMgr).FirstOrDefault();
                    if (ProductionMgr != null)
                    {
                        production_mgr_email = getEmail(ProductionMgr);//mail to mgr. dept qc
                    }
                    else
                    {
                        ProductionMgr = (from c in tnc.View_Organization
                                 where c.plant_id == production_emp.plant_id
                                 select c.PlantMgr).FirstOrDefault();
                        if (ProductionMgr != null)
                        {
                            production_mgr_email = getEmail(ProductionMgr);//mail to mgr. plant qc

                        }
                    }
                }


                if (IPQC.EngMgr != null)
                {
                    mgr_eng_email = getEmail(IPQC.EngMgr);//cc mgr. eng
                    ccList.Add(mgr_eng_email);

                }


                var eng_email = getEmail(IPQC.IssueBy);//cc eng in-charge
                ccList.Add(eng_email);

                var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
                var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
                var sup_eng_email = "";
                if (sup_eng != null)
                {
                    foreach (var sub_emp in sup_eng)
                    {
                        sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                        if (sup_eng_email != "")
                        {
                            ccList.Add(sup_eng_email);
                        }

                    }
                }

                ccmail = string.Join(",", ccList);

                var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
                var textMail = "";
                var body = "";
                textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " waiting for Comment & sign</b>";
                body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href = '" + "http://localhost:58735/Home/Information?id=" + IPQC.IPQCNo + @"' > Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

                //cUtil.sendEMail(production_mgr_email, title, body, ccmail);

                db.SaveChanges();

                
             
                return Json(1);
            }
            else
            {
                return Json(0);
            }
        }


        public ActionResult ProductionConcern(string IPQCNo, string ProductionComment)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();
            if (IPQC != null)
            {
                IPQC.StatusId = 12;

                //IPQC Log
                IPQCLog log = new IPQCLog();
                log.IPQCNo = IPQCNo;
                log.StatusId = 11;
                log.EntryDate = DateTime.Now;
                log.EntryBy = Session["IPQCUsr"].ToString();
                log.Comment = ProductionComment;

                db.IPQCLogs.Add(log);

                //// Mail Section

                List<string> ccList = new List<string>();
                var ccmail = "";
                var mgr_eng_email = "";
                var pe_mgr_email = "";
                var pe_emp = tnc.V_Employee_Info.Where(w => w.group_id == IPQC.PE_Group_id).FirstOrDefault();
                var PEMgr = (from c in tnc.View_Organization
                                     where c.group_id == pe_emp.group_id
                                     select c.GroupMgr).FirstOrDefault();
                if (PEMgr != null)
                {
                    pe_mgr_email = getEmail(PEMgr);//mail to mgr. group qc
                }
                else
                {
                    PEMgr = (from c in tnc.View_Organization
                                     where c.dept_id == pe_emp.dept_id
                                     select c.DeptMgr).FirstOrDefault();
                    if (PEMgr != null)
                    {
                        pe_mgr_email = getEmail(PEMgr);//mail to mgr. dept qc
                    }
                    else
                    {
                        PEMgr = (from c in tnc.View_Organization
                                         where c.plant_id == pe_emp.plant_id
                                         select c.PlantMgr).FirstOrDefault();
                        if (PEMgr != null)
                        {
                            pe_mgr_email = getEmail(PEMgr);//mail to mgr. plant qc

                        }
                    }
                }


                if (IPQC.EngMgr != null)
                {
                    mgr_eng_email = getEmail(IPQC.EngMgr);//cc mgr. eng
                    ccList.Add(mgr_eng_email);

                }


                var eng_email = getEmail(IPQC.IssueBy);//cc eng in-charge
                ccList.Add(eng_email);

                var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
                var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
                var sup_eng_email = "";
                if (sup_eng != null)
                {
                    foreach (var sub_emp in sup_eng)
                    {
                        sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                        if (sup_eng_email != "")
                        {
                            ccList.Add(sup_eng_email);
                        }

                    }
                }

                ccmail = string.Join(",", ccList);

                var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
                var textMail = "";
                var body = "";
                textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " waiting for Comment & sign</b>";
                body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href = '" + "http://localhost:58735/Home/Information?id=" + IPQC.IPQCNo + @"' > Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

                //cUtil.sendEMail(pe_mgr_email, title, body, ccmail);

                db.SaveChanges();
                return Json(1);
            }
            else
            {
                return Json(0);
            }
        }

        public ActionResult PEConcern(string IPQCNo, string PEComment, string Action)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();
            if (IPQC != null)
            {
                

                var pe_log = db.IPQCLogs.Find(IPQCNo,12);

                if(pe_log == null)
                {
                    IPQCLog log = new IPQCLog();
                    log.IPQCNo = IPQCNo;
                    log.StatusId = 12;
                    log.EntryDate = DateTime.Now;
                    log.EntryBy = Session["IPQCUsr"].ToString();
                    log.Comment = PEComment;

                    db.IPQCLogs.Add(log);
                }
                else
                {
                    pe_log.EntryDate = DateTime.Now;
                    pe_log.EntryBy = Session["IPQCUsr"].ToString();
                    pe_log.Comment = PEComment;
                }

                if (Action ==  "Approve")
                {
                    IPQC.StatusId = 13;
                }
                //IPQC Log


                //// Mail Section

                List<string> ccList = new List<string>();
                var ccmail = "";
                var mgr_eng_email = "";
                var pp_mgr_email = "";
                var pp_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.PP).FirstOrDefault();
                var PPMgr = (from c in tnc.View_Organization
                             where c.group_id == pp_emp.group_id
                             select c.GroupMgr).FirstOrDefault();
                if (PPMgr != null)
                {
                    pp_mgr_email = getEmail(PPMgr);//mail to mgr. group qc
                }
                else
                {
                    PPMgr = (from c in tnc.View_Organization
                             where c.dept_id == pp_emp.dept_id
                             select c.DeptMgr).FirstOrDefault();
                    if (PPMgr != null)
                    {
                        pp_mgr_email = getEmail(PPMgr);//mail to mgr. dept qc
                    }
                    else
                    {
                        PPMgr = (from c in tnc.View_Organization
                                 where c.plant_id == pp_emp.plant_id
                                 select c.PlantMgr).FirstOrDefault();
                        if (PPMgr != null)
                        {
                            pp_mgr_email = getEmail(PPMgr);//mail to mgr. plant qc

                        }
                    }
                }


                if (IPQC.EngMgr != null)
                {
                    mgr_eng_email = getEmail(IPQC.EngMgr);//cc mgr. eng
                    ccList.Add(mgr_eng_email);

                }


                var eng_email = getEmail(IPQC.IssueBy);//cc eng in-charge
                ccList.Add(eng_email);

                var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
                var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
                var sup_eng_email = "";
                if (sup_eng != null)
                {
                    foreach (var sub_emp in sup_eng)
                    {
                        sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                        if (sup_eng_email != "")
                        {
                            ccList.Add(sup_eng_email);
                        }

                    }
                }

                ccmail = string.Join(",", ccList);

                var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
                var textMail = "";
                var body = "";
                textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " waiting for Comment & sign</b>";
                body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href = '" + "http://localhost:58735/Home/Information?id=" + IPQC.IPQCNo + @"' > Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

                //cUtil.sendEMail(pp_mgr_email, title, body, ccmail);

                db.SaveChanges();
                return Json(1);
            }
            else
            {
                return Json(0);
            }
        }

        public ActionResult PPConcern(string IPQCNo, string PPComment)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();
            if (IPQC != null)
            {
                IPQC.StatusId = 14;

                //IPQC Log
                IPQCLog log = new IPQCLog();
                log.IPQCNo = IPQCNo;
                log.StatusId = 13;
                log.EntryDate = DateTime.Now;
                log.EntryBy = Session["IPQCUsr"].ToString();
                log.Comment = PPComment;

                db.IPQCLogs.Add(log);

                //// Mail Section

                List<string> ccList = new List<string>();
                var ccmail = "";
                var mgr_eng_email = "";
                var pln_mgr_email = "";
                var pln_emp = tnc.V_Employee_Info.Where(w => w.group_id == IPQC.PLN_groupid).FirstOrDefault();
                var PLNMgr = (from c in tnc.View_Organization
                             where c.group_id == pln_emp.group_id
                             select c.GroupMgr).FirstOrDefault();
                if (PLNMgr != null)
                {
                    pln_mgr_email = getEmail(PLNMgr);//mail to mgr. group qc
                }
                else
                {
                    PLNMgr = (from c in tnc.View_Organization
                             where c.dept_id == pln_emp.dept_id
                             select c.DeptMgr).FirstOrDefault();
                    if (PLNMgr != null)
                    {
                        pln_mgr_email = getEmail(PLNMgr);//mail to mgr. dept qc
                    }
                    else
                    {
                        PLNMgr = (from c in tnc.View_Organization
                                 where c.plant_id == pln_emp.plant_id
                                 select c.PlantMgr).FirstOrDefault();
                        if (PLNMgr != null)
                        {
                            pln_mgr_email = getEmail(PLNMgr);//mail to mgr. plant qc
                        }
                    }
                }


                if (IPQC.EngMgr != null)
                {
                    mgr_eng_email = getEmail(IPQC.EngMgr);//cc mgr. eng
                    ccList.Add(mgr_eng_email);

                }


                var eng_email = getEmail(IPQC.IssueBy);//cc eng in-charge
                ccList.Add(eng_email);

                var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
                var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
                var sup_eng_email = "";
                if (sup_eng != null)
                {
                    foreach (var sub_emp in sup_eng)
                    {
                        sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                        if (sup_eng_email != "")
                        {
                            ccList.Add(sup_eng_email);
                        }

                    }
                }

                ccmail = string.Join(",", ccList);

                var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
                var textMail = "";
                var body = "";
                textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " waiting for Comment & sign</b>";
                body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href = '" + "http://localhost:58735/Home/Information?id=" + IPQC.IPQCNo + @"'> Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

                //cUtil.sendEMail(pln_mgr_email, title, body, ccmail);

                db.SaveChanges();
                return Json(1);
            }
            else
            {
                return Json(0);
            }
        }

        public ActionResult PLNConcern(string IPQCNo, string PLNComment)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();
            if (IPQC != null)
            {
                IPQC.StatusId = 15;

                //IPQC Log
                IPQCLog log = new IPQCLog();
                log.IPQCNo = IPQCNo;
                log.StatusId = 14;
                log.EntryDate = DateTime.Now;
                log.EntryBy = Session["IPQCUsr"].ToString();
                log.Comment = PLNComment;

                db.IPQCLogs.Add(log);

                //// Mail Section

                List<string> ccList = new List<string>();
                List<string> mail_toList = new List<string>();
                var ccmail = "";
                var mgr_eng_email = "";
                var mail_to = "";
                
                if (IPQC.EngMgr != null)
                {
                    mgr_eng_email = getEmail(IPQC.EngMgr);//cc mgr. eng
                    ccList.Add(mgr_eng_email);

                }

                var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
                var eng_group = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_status == "A").ToList();

                var eng_group_email = "";
                foreach (var item in eng_group)
                {
                    eng_group_email = getEmail(item.emp_code);
                    if (eng_group_email != null)
                    {
                        mail_toList.Add(eng_group_email);
                    }
                }

                mail_to = string.Join(",", mail_toList);

                var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
                var sup_eng_email = "";
                if (sup_eng != null)
                {
                    foreach (var sub_emp in sup_eng)
                    {
                        sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                        if (sup_eng_email != "")
                        {
                            ccList.Add(sup_eng_email);
                        }

                    }
                }

                ccmail = string.Join(",", ccList);

                var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
                var textMail = "";
                var body = "";
                textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " for review & comment</b>";
                body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href = '" + "http://localhost:58735/Home/Information?id=" + IPQC.IPQCNo + @"'> Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

                //cUtil.sendEMail(mail_to, title, body, ccmail);

                db.SaveChanges();

                return Json(1);
            }
            else
            {
                return Json(0);
            }
        }

        public ActionResult EngReview(string IPQCNo, string EngComment)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();
            if (IPQC != null)
            {
                IPQC.StatusId = 5;

                //IPQC Log
                IPQCLog log = new IPQCLog();
                log.IPQCNo = IPQCNo;
                log.StatusId = 15;
                log.EntryDate = DateTime.Now;
                log.EntryBy = Session["IPQCUsr"].ToString();
                log.Comment = EngComment;

                db.IPQCLogs.Add(log);

                //// Mail Section

                List<string> ccList = new List<string>();
                var ccmail = "";
                var mgr_eng_email = "";

                if (IPQC.EngMgr != null)
                {
                    mgr_eng_email = getEmail(IPQC.EngMgr);//mail to mgr. eng

                }

                var eng_email = getEmail(IPQC.IssueBy);//cc eng in-charge
                ccList.Add(eng_email);

                var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
                var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
                var sup_eng_email = "";
                if (sup_eng != null)
                {
                    foreach (var sub_emp in sup_eng)
                    {
                        sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                        if (sup_eng_email != "")
                        {
                            ccList.Add(sup_eng_email);
                        }

                    }
                }

                ccmail = string.Join(",", ccList);

                var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
                var textMail = "";
                var body = "";
                textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " waiting for Approve</b>";
                body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href = '" + "http://localhost:58735/Home/Information?id=" + IPQC.IPQCNo + @"'> Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

                //cUtil.sendEMail(mgr_eng_email, title, body, ccmail);

                db.SaveChanges();

                return Json(1);
            }
            else
            {
                return Json(0);
            }
        }

        public ActionResult CloseIPQC(string IPQCNo, string Action, string EngMgrComment)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            var IPQC = db.IPQCHeads.Where(w => w.IPQCNo == IPQCNo).FirstOrDefault();
            if (IPQC != null)
            {

                IPQC.StatusId = 9;
                IPQC.EngMgrResult = Action;

                //IPQC Log
                IPQCLog log = new IPQCLog();
                log.IPQCNo = IPQCNo;
                log.StatusId = 9;
                log.EntryDate = DateTime.Now;
                log.EntryBy = Session["IPQCUsr"].ToString();
                log.Comment = EngMgrComment;

                db.IPQCLogs.Add(log);


                //// Mail Section

                List<string> ccList = new List<string>();
                var ccmail = "";
           

                var eng_email = getEmail(IPQC.IssueBy);//mail to eng in-charge


                var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == IPQC.IssueBy).FirstOrDefault();
                var eng_group = tnc.V_Employee_Info.Where(w => w.group_id == eng_emp.group_id && w.emp_status == "A").ToList();

                var eng_group_email = "";
                foreach (var item in eng_group)
                {
                    eng_group_email = getEmail(item.emp_code);
                    if (eng_group_email != null)
                    {
                        ccList.Add(eng_group_email);
                    }
                }


                ccmail = string.Join(",", ccList);

                var title = "E-mail IPQC (IPQC No. : " + IPQC.IPQCNo + ")";
                var textMail = "";
                var body = "";
                if(Action == "OK")
                {
                    textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " was Completed and judgment <font color=#00B050>OK</font></b>";
                }
                else if (Action == "NG")
                {
                    textMail = "<b>You have the IPQC online of Item " + IPQC.ItemCode + " was Completed and judgment <font color=#FF0000>Re-IPQC</font></b>";
                }

                body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + IPQC.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + IPQC.IPQCNo + @"<br></dd>
                            <dd> Item. : " + IPQC.ItemCode + @"<br></dd>
                            <dd> Sampling : " + IPQC.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href = '" + "http://localhost:58735/Home/Information?id=" + IPQC.IPQCNo + @"'> Internal </a><br></dd >		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";

                //cUtil.sendEMail(eng_email, title, body, ccmail);

                db.SaveChanges();

                return Json(1);
            }
            else
            {
                return Json(0);
            }

        }

        public ActionResult CancelIPQC(string IPQCNo, string CancelReason)
        {
            if (chkSession()) return Json(-1);
            var ipqc = db.IPQCHeads.Find(IPQCNo);
            if (ipqc != null)
            {
                try
                {
                    ipqc.StatusId = 8;
                    ipqc.CancelReason = CancelReason;

                    // IPQC Log
                    IPQCLog log = new IPQCLog();
                    log.IPQCNo = IPQCNo;
                    log.StatusId = 8;
                    log.EntryDate = DateTime.Now;
                    log.EntryBy = Session["IPQCUsr"].ToString();

                    db.IPQCLogs.Add(log);

                    db.SaveChanges();

                    return Json(1);
                }
                catch (Exception)
                {
                    return Json(0);
                }

            }
            else
            {
                return Json(0);
            }
        
        }

        public ActionResult GetStatus(string ipqcNo)
        {
            var status = db.IPQCHeads.Where(w => w.IPQCNo == ipqcNo).Select(s => s.Status.Detail).FirstOrDefault();
            return Json(status, JsonRequestBehavior.AllowGet);
        }

        public ActionResult TellIssuer(string id, string comment)
        {
            if (chkSession()) return Json(-1);

            var ipqc = db.IPQCHeads.Find(id);
            if (ipqc != null)
            {
                try
                {
                    //// Mail Section
                    var emp = tnc.V_Employee_Info.Where(w => w.emp_code == ipqc.IssueBy).FirstOrDefault();
                    List<string> ccList = new List<string>();
                    var ccmail = "";
                    var eng_email = getEmail(ipqc.Engineer); //mail to eng

                    if (ipqc.EngMgr != null)
                    {
                        var mgr_eng_email = getEmail(ipqc.EngMgr);//cc mgr. eng
                        ccList.Add(mgr_eng_email);
                    }

                    var sup_eng = tnc.V_Employee_Info.Where(w => w.group_id == emp.group_id && w.emp_position == 5 && w.emp_status == "A").ToList();
                    var sup_eng_email = "";
                    if (sup_eng != null)
                    {
                        foreach (var sub_emp in sup_eng)
                        {
                            sup_eng_email = getEmail(sub_emp.emp_code);//cc sup. eng
                            ccList.Add(sup_eng_email);
                        }
                    }
                    ccmail = string.Join(",", ccList);
                    var title = "E-mail IPQC (IPQC No. : " + ipqc.IPQCNo + ")";
                    var textMail = "";
                    var body = "";
                    textMail = "<b>You have the IPQC online of Item " + ipqc.ItemCode + " Tell Issuer \"" + comment + "\" to you</b>";
                    body = @"<html><body><dd><b>Dear Sir,</b><br><br></dd>
                            <dd>" + textMail + @"<br></dd>
                            <dd> Topic : " + ipqc.Purpose + @"<br></dd>
                            <dd> IPQC No. : " + ipqc.IPQCNo + @"<br></dd>
                            <dd> Item. : " + ipqc.ItemCode + @"<br></dd>
                            <dd> Sampling : " + ipqc.Sampling + @"<br></dd>
                            <dd> Link : " + @" <a href='" + "http://localhost:58735/Home/Edit?id=" + ipqc.IPQCNo + @"'> Internal </a><br></dd>		
                            <dd><small>---------------------------------------------------------------------------------------------------------------------------</small><br></dd>
                            <dd><small><font color=#FF0000> **This email send automatic from IPQC**</font></small><br></dd></body></html>";



                    //cUtil.sendEMail(eng_email, title, body, ccmail);

                    return Json(1);
                }
                catch (Exception)
                {
                    return Json(0);
                }
            }
            else
            {
                return Json(0);
            }
        }

        [HttpPost]
        public ActionResult ExportList(string sidx, string sord, string IPQCNo, string itemCode, byte? TypeId, byte? StatusId, string Issuer, string JobNo, string PPDDL, string QCDDL)
        {
            var data = db.IPQCHeads.OrderBy(sidx + " " + sord);

            // Filter by search condition
            if (!string.IsNullOrEmpty(IPQCNo))
                data = data.Where(d => d.IPQCNo == IPQCNo);

            if (!string.IsNullOrEmpty(itemCode))
                data = data.Where(d => d.ItemCode.Contains(itemCode));

            if (TypeId != null)
                data = data.Where(d => d.TypeId == TypeId);

            if (StatusId != null)
                data = data.Where(d => d.StatusId == StatusId);

            if (!string.IsNullOrEmpty(Issuer))
                data = data.Where(d => d.IssueBy == Issuer);

            if (!string.IsNullOrEmpty(PPDDL))
                data = data.Where(d => d.PP == PPDDL);

            if (!string.IsNullOrEmpty(QCDDL))
            {
                var QCGroup_Id = int.Parse(QCDDL);
                data = data.Where(d => d.QCGroup == QCGroup_Id);
            }

            if (!string.IsNullOrEmpty(JobNo))
            {
                var job = JobNo.Split('-')[0];
                data = data.Where(d => d.IPQCLots.FirstOrDefault().JobNo.Contains(job));
            }

            var selectData = data.ToList().Select(d => new ExportList()
            {
                IPQCNo = d.IPQCNo,
                Lot = db.IPQCProcesses.Where(w => w.IPQCNo == d.IPQCNo && w.ProcessId == 9).Count() + "/" + d.LotQty,
                Purpose = d.Purpose,
                ItemCode = d.ItemCode,
                type = d.Type.Detail,
                IssueDate = d.EntryDate,
                status = d.Status.Detail,
                issueby = tnc.V_Employee_Info.Where(w => w.emp_code == d.IssueBy).Select(s => new { fullname = s.emp_fname + " " + s.emp_lname }).FirstOrDefault().fullname,
                PP = tnc.V_Employee_Info.Where(w => w.emp_code == d.PP).Select(s => new { fullname = s.emp_fname + " " + s.emp_lname }).FirstOrDefault().fullname,
                QC = tnc.View_Organization.Where(w => w.group_id == d.QCGroup).Select(s => s.group_name).FirstOrDefault(),
            });
            ViewBag.Data = selectData;

            return View();
        }

        public ActionResult Information(string id)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            ViewBag.Title = id;
            ViewBag.Message = "IPQC Information";
            var ipqc = db.IPQCHeads.Find(id);

            ViewBag.IPQC = ipqc;

            // Type DropDownList
            ViewBag.Type = db.Types;

            // Type Rank
            ViewBag.Rank = db.TM_Rank;

            // Engineer DropDownList
            ViewBag.Engineer = from e in tnc.V_Employee_Info
                               where e.plant_id == 10 && e.position_level <= 3 && e.emp_status == "A"
                               orderby e.group_name
                               select e;

            // PP DropDownList
            var PP = from e in tnc.V_Employee_Info
                     where (e.dept_id == 37 && e.group_id != 0 && (e.emp_position == 3 || e.emp_position == 25 || e.emp_position == 23) && e.emp_status == "A")
                     orderby e.group_name, e.emp_fname, e.emp_lname
                     select e;

            foreach (var item in PP)
            {
                if (item.emp_code == ipqc.PP)
                {
                    ViewBag.PPGroup = item.group_id;
                }

            }

            ViewBag.PP = PP;

            // QC Dropdown
            ViewBag.QCGroup = tnc.View_Organization.Where(w => w.plant_id == 11 && w.group_id != 0 && w.active == true)
                .GroupBy(g => new { g.group_id, g.group_name })
                .Select(s => s.FirstOrDefault())
                .OrderBy(o => o.group_name)
                .ToList();

            // ProductionGroup Dropdown
            ViewBag.ProductionGroup = db.P_SelectProductionGroup()
                .GroupBy(g => new { g.group_id, g.group_name })
                .Select(s => s.FirstOrDefault())
                .OrderBy(o => o.group_name)
                .ToList();

            // ADD NEW
            ViewBag.PLN = (from e in tnc.V_Employee_Info
                           where ((e.group_id == 14 || e.group_id == 95 || e.group_id == 105 || e.group_id == 148) && e.group_id != 0 && e.emp_status == "A")
                           select e)
                      .GroupBy(g => new { g.group_id, g.group_name })
                      .Select(s => s.FirstOrDefault())
                      .OrderBy(o => o.group_name)
                      .ToList();

            ViewBag.PE = from e in tnc.V_Employee_Info
                         where (e.plant_id == 10 && e.group_id != 0 && e.emp_status == "A" && e.position_level <= 3)
                         orderby e.group_name, e.emp_fname, e.emp_lname
                         select e;


            ViewBag.QCLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 4).FirstOrDefault();

            ViewBag.ProductionLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 11).FirstOrDefault();

            ViewBag.PELogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 12).FirstOrDefault();

            ViewBag.PPLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 13).FirstOrDefault();

            ViewBag.PLNLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 14).FirstOrDefault();

            ViewBag.EngLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 15).FirstOrDefault();

            ViewBag.EngMgrLogs = db.IPQCLogs.Where(e => e.IPQCNo == id && e.StatusId == 9).FirstOrDefault();




            var ipqcLog = (from d in ipqc.IPQCLogs.ToList()
                           select d).OrderBy(o => o.EntryDate).ToList();

            //show latest information (if latest status is cancel show previous information before cancel)
            if (ipqcLog[ipqcLog.Count - 1].StatusId == 8)
            {
                ViewBag.stepCount = ipqcLog.Count - 1;
            }
            else
            {
                ViewBag.stepCount = ipqcLog.Count;
            }


            if (ipqc.IssueBy != null)
            {
                var eng_emp = tnc.V_Employee_Info.Where(w => w.emp_code == ipqc.IssueBy).FirstOrDefault();
                ViewBag.eng_dept = eng_emp.dept_id;
                ViewBag.eng_plant = eng_emp.plant_id;
            }

            if (ipqc.QCGroup != null)
            {
                var qc_emp = tnc.V_Employee_Info.Where(w => w.group_id == ipqc.QCGroup).FirstOrDefault();
                ViewBag.qc_dept = qc_emp.dept_id;
                ViewBag.qc_plant = qc_emp.plant_id;
            }

            if (ipqc.production_groupid != null)
            {
                var production_emp = tnc.V_Employee_Info.Where(w => w.group_id == ipqc.production_groupid).FirstOrDefault();
                ViewBag.production_dept = production_emp.dept_id;
                ViewBag.production_plant = production_emp.plant_id;
            }

            if (ipqc.PE_Group_id != null)
            {
                var pe_emp = tnc.V_Employee_Info.Where(w => w.group_id == ipqc.PE_Group_id).FirstOrDefault();
                ViewBag.pe_dept = pe_emp.dept_id;
                ViewBag.pe_plant = pe_emp.plant_id;
            }

            if (ipqc.PP != null)
            {
                var pp_emp = tnc.V_Employee_Info.Where(w => w.emp_code == ipqc.PP).FirstOrDefault();
                ViewBag.pp_group = pp_emp.group_id;
                ViewBag.pp_dept = pp_emp.dept_id;
                ViewBag.pp_plant = pp_emp.plant_id;
            }


            if (ipqc.PLN_groupid != null)
            {
                var pln_emp = tnc.V_Employee_Info.Where(w => w.group_id == ipqc.PLN_groupid).FirstOrDefault();
                ViewBag.pln_dept = pln_emp.dept_id;
                ViewBag.pln_plant = pln_emp.plant_id;
            }

            List<Log> lstLog = new List<Log>();
            Log objLog;
            var tmpLog = (from d in ipqc.IPQCLogs.ToList()
                          join t in tnc.V_Employee_Info.ToList()
                          on d.EntryBy equals t.emp_code into j
                          from t in j.DefaultIfEmpty()
                          select new
                          {
                              logID = d.StatusId,
                              operation = d.Status.Detail,
                              opdate = d.EntryDate,
                              actor = t != null ? t.emp_fname + " " + t.emp_lname : d.EntryBy,
                              comment = d.Comment
                          }).OrderBy(o => o.opdate);


            foreach (var item in tmpLog)
            {
                objLog = new Log();
                if (item.operation.StartsWith("Waiting for "))
                {
                    objLog.operation = item.operation.Substring(12);
                }
                else
                {
                    objLog.operation = item.operation;
                }
                objLog.opdate = item.opdate;
                objLog.actor = item.actor;
                objLog.comment = item.comment;

                lstLog.Add(objLog);
            }
            ViewBag.log = lstLog;

            List<Lots> lstLots = new List<Lots>();
            Lots objLot;
            var ipqc_lots = (from l in db.IPQCLots where l.IPQCNo == id orderby l.Lot select l).ToList();

            if(ipqc_lots.Count() > 0)
            {
           
                    
                    foreach (var item in ipqc_lots)
                    {
                        objLot = new Lots();
                        objLot.Lot = item.Lot;
                        objLot.JobNo = item.JobNo;
                        objLot.TagNo = item.TagNo;
                        objLot.job_Lot = item.job_Lot;
                        objLot.StartDate = item.StartDate;
                        objLot.ExpireDate = item.ExpireDate;
                        objLot.Job_STS = item.Job_STS;
                        if (db.IPQCProcesses.Where(p => p.IPQCNo == id).FirstOrDefault() != null)
                        {
                            var process_id = item.IPQCProcesses.Max(m => m.ProcessId);
                            var process_status = db.Processes.Where(p => p.Id == process_id).Select(s => s.Detail).FirstOrDefault();
                            objLot.Process_STS = process_status;
                        }
                        else
                        {
                            objLot.Process_STS = null;
                        }
                        lstLots.Add(objLot);
                    }
        
                ViewBag.lots = lstLots;

            }
            else
            {
                ViewBag.lots = null;
            }


  


            return View(ipqc);
        }


        public ActionResult Judgement(string id)
        {
            if (chkSession()) return RedirectToAction("Login", "Member");
            ViewBag.Title = id;
            ViewBag.Message = "IPQC Review Meeting Record";
            var ipqc = db.IPQCHeads.Find(id);

            // Type Rank
            ViewBag.Rank = db.TM_Rank;

            // Engineer DropDownList
            ViewBag.Engineer = from e in tnc.V_Employee_Info
                               where e.plant_id == 10 && e.position_level <= 3 && e.emp_status == "A"
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

            return View(ipqc);
        }

    }
}
