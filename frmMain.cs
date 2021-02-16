using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Oracle.ManagedDataAccess.Client;
using System.IO;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Base;

namespace NagarryFrameworkQueryBackup
{
    public partial class frmMain : Form
    {

        internal OracleConnection mOraSourceConn = null;    //소스 커넥션
        internal String mDBUser, mDBPwd, mDBSource, mFrameworkUserID;

        private String mReturnType = "";
        private String mMaxKB = "4096";

        private const String PRG_TYPE_01 = "MenTalCrush";
        private const String PRG_TYPE_02 = "Oracle Function";
        private const String PRG_TYPE_03 = "Java Bean";

        private const String OP_TYPE_01 = "Insert";
        private const String OP_TYPE_02 = "Update";
        private const String OP_TYPE_03 = "Select";
        private const String OP_TYPE_04 = "Delete";
        private const String OP_TYPE_05 = "Batch";
        private const String OP_TYPE_06 = "Page";

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            initControls();
            LoadConfig();
        }

        void initControls()
        {
            dpSDTE.Text = DateTime.Now.AddDays(-7).ToShortDateString();
            dpEDTE.Text = DateTime.Now.ToShortDateString();

            grdDataView.OptionsBehavior.Editable = false;
            grdDataView.OptionsBehavior.ReadOnly = true;

            cmbType.Items.Add(PRG_TYPE_01);
            cmbType.Items.Add(PRG_TYPE_02);
            cmbType.Items.Add(PRG_TYPE_03);
            cmbType.SelectedIndex = 0;

            cmbOperation.Items.Add(OP_TYPE_01);
            cmbOperation.Items.Add(OP_TYPE_02);
            cmbOperation.Items.Add(OP_TYPE_03);
            cmbOperation.Items.Add(OP_TYPE_04);
            cmbOperation.Items.Add(OP_TYPE_05);
            cmbOperation.Items.Add(OP_TYPE_06);
            cmbType.SelectedIndex = 0;

            setEnableScriptButton();
        }

        void LoadConfig()
        {
            StreamReader sini = null;

            try
            {
                sini = new StreamReader("Nagarry.ini", Encoding.Default);
                mDBUser = sini.ReadLine();
                mDBPwd = sini.ReadLine();
                mDBSource = sini.ReadLine();
                mFrameworkUserID = sini.ReadLine();
                sini.Close();
                getConnectionString(mDBUser, mDBPwd, mDBSource);
                ConnectionDB();
            }
            catch (IOException iex)
            {
                MessageBox.Show(iex.Message, "설정파일 생성실패");
            }
            finally
            {
            }
        }

        #region MakeScript
        private void btnDataBase_Click(object sender, EventArgs e)
        {
            frmConfig frmCfg = new frmConfig();
            frmCfg.ShowDialog(this);
        }

        internal void ConnectionDB()
        {
            Program.mainForm.mOraSourceConn = new OracleConnection(getConnectionString(mDBUser, mDBPwd, mDBSource));
        }

        private void btnGenerator_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            getProgramList_Date();
            setEnableScriptButton();

            Cursor.Current = Cursors.Default;
        }

        private void setEnableScriptButton()
        {
            btnMakeScript.Enabled = grdDataView.DataRowCount > 0 ? true : false;
            if (btnMakeScript.Enabled)
                btnMakeScript.BackColor = Color.Red;
            else
                btnMakeScript.BackColor = Color.Gray;
        }

        private void getProgramList_Date()
        {
            //List<ProgramList> programIdList = new List<ProgramList>();

            if (mOraSourceConn == null)
                return;

            try
            {
                String sUpdatedListQuery = String.Format(@"with VAL as
    (select to_date('{0} 000000', 'yyyy-mm-dd hh24:mi:ss') FROM_DTE,
    TO_DATE('{1} 235959', 'yyyy-mm-dd hh24:mi:ss') TO_DTE,
    '{2}' FID
    from DUAL
    )
    select PROGRAM_ID,
    PRG_TYPE,
    to_char(REG_DATE, 'yyyy-mm-dd hh24:mi:ss') REG_DATE,
    REG_ID,
    to_char(MOD_DATE, 'yyyy-mm-dd hh24:mi:ss') MOD_DATE,
    MOD_ID
    from FRM_PRG F,
    VAL V
    where ((REG_DATE between V.FROM_DTE and V.TO_DTE and REG_ID = V.FID)
    or (MOD_DATE between V.FROM_DTE and V.TO_DTE and MOD_ID = V.FID)) order by REG_DATE",
                    dpSDTE.Text.Replace("-", ""), dpEDTE.Text.Replace("-", ""), mFrameworkUserID);

                mOraSourceConn.Open();
                DataSet ds = new DataSet();
                OracleDataAdapter da = new OracleDataAdapter(sUpdatedListQuery, mOraSourceConn);
                da.Fill(ds, "myPrgList");

                grdData.DataSource = ds.Tables["myPrgList"];
                grdDataView.BestFitColumns();
            }
            catch (OracleException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                mOraSourceConn.Close();
            }
        }

        internal String getConnectionString(String pSourceUser, String pSourcePwd, String pSourceDB)
        {
            String strReturn = "";
            StringBuilder strConnect = new StringBuilder();

            strConnect.AppendFormat("User Id={0};Password={1};Data Source={2}", pSourceUser, pSourcePwd, pSourceDB).ToString();
            strReturn = strConnect.ToString();

            return strReturn;
        }

        private void btnMakeScript_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            String sProgramIDs = getProgramIDList();

            try
            {
                mOraSourceConn.Open();
                String sDate = DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString().PadLeft(2, '0') + DateTime.Now.Day.ToString().PadLeft(2, '0');
                String sFileName = sDate + "FRM_" + mFrameworkUserID + "_[" + dpSDTE.Text + "][" + dpEDTE.Text + "].sql";

                if (SaveFrmCode(sProgramIDs, sFileName))
                {
                    if (SaveFrmPrg(sProgramIDs, sFileName))
                    {
                        if (saveFrmPrm(sProgramIDs, sFileName))
                            MessageBox.Show("Script File is created");
                    }
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                mOraSourceConn.Close();
            }

            Cursor.Current = Cursors.Default;
        }

        private String getProgramIDList()
        {
            String sRst = "(";

            try
            {
                for (int i = 0; i < grdDataView.RowCount; i++)
                {
                    sRst += i == 0 ? "" : ",";
                    sRst += "'" + grdDataView.GetRowCellValue(i, "PROGRAM_ID") + "'";
                }
                sRst += ")";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return sRst;
        }

        private Boolean SaveFrmCode(String sProgramIDs, String sFileName)
        {
            Boolean bRst = false;

            try
            {
                String sUpdatedListQuery = String.Format(@"select
    'merge into FRM_CODE a
    using (select '''
    || CODE_GBN ||''' CODE_GBN,'
    ||''''|| CODE ||''' CODE,'
    ||''''|| CODE_NM ||''' CODE_NM,'
    ||''''|| P_CODE ||''' P_CODE,'
    ||' to_date('''|| to_char(REG_DATE, 'yyyy-mm-dd hh24:mi:ss') ||''', ''yyyy-mm-dd hh24:mi:ss'') REG_DATE,'
    ||''''|| DEPTH || '''' ||' DEPTH from dual) B
    on (a.CODE in B.CODE)
    when not matched then
        insert
        (CODE_GBN, CODE, CODE_NM, P_CODE, REG_DATE, DEPTH)
        values
        (B.CODE_GBN, B.CODE, B.CODE_NM, B.P_CODE, B.REG_DATE, B.DEPTH);'
    from FRM_CODE
    where CODE in {0}", sProgramIDs);

                DataSet ds = new DataSet();
                OracleDataAdapter da = new OracleDataAdapter(sUpdatedListQuery, mOraSourceConn);
                da.Fill(ds, "myCode");

                if(SaveScriptFile("set define off;", sFileName, false))
                    bRst = SaveScriptFile(ds.Tables["myCode"], sFileName, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                bRst = false;
            }

            return bRst;
        }

        private Boolean SaveFrmPrg(String sProgramIDs, String sFileName)
        {
            Boolean bRst = false;

            try
            {
                /*String sUpdatedListQuery = String.Format(@"select
    'merge into FRM_PRG A
    using (select '''|| PROGRAM_ID ||''' PROGRAM_ID,'
    ||''''|| PRG_TYPE ||''' PRG_TYPE,'
    ||''''|| PRG_NAME ||''' PRG_NAME,'
    ||''''|| OP_TYPE ||''' OP_TYPE,'
    ||''''|| replace(nvl(SQL, ' '), '''', '''''') ||''' SQL,'
    ||''''|| CLASS_NAME ||''' CLASS_NAME,'
    ||''''|| METHOD_NAME ||''' METHOD_NAME,'
    ||''''|| RETURN_TYPE ||''' RETURN_TYPE,'
    ||''''|| MAX_KB ||''' MAX_KB,'
    ||' to_date('''|| to_char(REG_DATE, 'yyyy-mm-dd hh24:mi:ss') ||''', ''yyyy-mm-dd hh24:mi:ss'') REG_DATE,'
    ||''''|| REG_ID ||''' REG_ID,'
    ||' to_date('''|| TO_CHAR(MOD_DATE, 'yyyy-mm-dd hh24:mi:ss') ||''', ''yyyy-mm-dd hh24:mi:ss'') MOD_DATE,'
    ||''''|| MOD_ID ||''' MOD_ID,'
    ||''''|| JNDI ||''' JNDI
    from dual) B
    on (A.PROGRAM_ID = B.PROGRAM_ID)
    when matched then
    update
    set A.PRG_NAME = B.PRG_NAME,
    A.SQL   = B.SQL,
    A.MOD_DATE = B.MOD_DATE,
    A.MOD_ID   = B.MOD_ID
    when not matched then
    insert
    (PROGRAM_ID,
    PRG_TYPE,
    PRG_NAME,
    OP_TYPE,
    SQL,
    CLASS_NAME,
    METHOD_NAME,
    RETURN_TYPE,
    REG_DATE,
    REG_ID,
    MOD_DATE,
    MOD_ID,
    MAX_KB,
    JNDI)
    values
    (B.PROGRAM_ID,
    B.PRG_TYPE,
    B.PRG_NAME,
    B.OP_TYPE,
    B.SQL,
    B.CLASS_NAME,
    B.METHOD_NAME,
    B.RETURN_TYPE,
    B.REG_DATE,
    B.REG_ID,
    B.MOD_DATE,
    B.MOD_ID,
    B.MAX_KB,
    B.JNDI);'
    from FRM_PRG
    where PROGRAM_ID in {0}", sProgramIDs);*/
                /*
                     ||''''|| replace(nvl(to_clob(SQL, 1, 4000), ' '), '''', '''''') ||''' SQL1,'
                ||''''|| replace(nvl(to_clob(SQL, 4001), ' '), '''', '''''') ||''' SQL2,'
                 * */
                String sUpdatedListQuery = String.Format(@"select
    'merge into FRM_PRG A
    using (select '''|| PROGRAM_ID ||''' PROGRAM_ID,'
    ||''''|| PRG_TYPE ||''' PRG_TYPE,'
    ||''''|| PRG_NAME ||''' PRG_NAME,'
    ||''''|| OP_TYPE ||''' OP_TYPE,'
    ||''''|| replace(nvl(to_clob(substr(SQL, 1, 3000)), ' '), '''', '''''') ||''' SQL1,'
    ||''''|| replace(to_clob(substr(SQL, 3001, 3000)), '''', '''''') ||''' SQL2,'
    ||''''|| replace(to_clob(substr(SQL, 6001, 3000)), '''', '''''') ||''' SQL3,'
    ||''''|| replace(to_clob(substr(SQL, 9001, 3000)), '''', '''''') ||''' SQL4,'
    ||''''|| replace(to_clob(substr(SQL, 12001, 3000)), '''', '''''') ||''' SQL5,'
    ||''''|| replace(to_clob(substr(SQL, 15001, 3000)), '''', '''''') ||''' SQL6,'
    ||''''|| replace(to_clob(substr(SQL, 18001, 3000)), '''', '''''') ||''' SQL7,'
    ||''''|| replace(to_clob(substr(SQL, 21001, 3000)), '''', '''''') ||''' SQL8,'
    ||''''|| replace(to_clob(substr(SQL, 24001, 3000)), '''', '''''') ||''' SQL9,'
    ||''''|| CLASS_NAME ||''' CLASS_NAME,'
    ||''''|| METHOD_NAME ||''' METHOD_NAME,'
    ||''''|| RETURN_TYPE ||''' RETURN_TYPE,'
    ||''''|| MAX_KB ||''' MAX_KB,'
    ||' to_date('''|| to_char(REG_DATE, 'yyyy-mm-dd hh24:mi:ss') ||''', ''yyyy-mm-dd hh24:mi:ss'') REG_DATE,'
    ||''''|| REG_ID ||''' REG_ID,'
    ||' to_date('''|| TO_CHAR(MOD_DATE, 'yyyy-mm-dd hh24:mi:ss') ||''', ''yyyy-mm-dd hh24:mi:ss'') MOD_DATE,'
    ||''''|| MOD_ID ||''' MOD_ID,'
    ||''''|| JNDI ||''' JNDI
    from dual) B
    on (A.PROGRAM_ID = B.PROGRAM_ID)
    when matched then
    update
    set A.PRG_NAME = B.PRG_NAME,
    A.SQL   = to_clob(B.SQL1) || to_clob(B.SQL2) || to_clob(B.SQL3) || to_clob(B.SQL4) || to_clob(B.SQL5),
    A.MOD_DATE = B.MOD_DATE,
    A.MOD_ID   = B.MOD_ID,
    A.PRG_TYPE = B.PRG_TYPE,
    A.OP_TYPE = B.OP_TYPE,
    A.RETURN_TYPE = B.RETURN_TYPE
    when not matched then
    insert
    (PROGRAM_ID,
    PRG_TYPE,
    PRG_NAME,
    OP_TYPE,
    SQL,
    CLASS_NAME,
    METHOD_NAME,
    RETURN_TYPE,
    REG_DATE,
    REG_ID,
    MOD_DATE,
    MOD_ID,
    MAX_KB,
    JNDI)
    values
    (B.PROGRAM_ID,
    B.PRG_TYPE,
    B.PRG_NAME,
    B.OP_TYPE,
    to_clob(B.SQL1) || to_clob(B.SQL2) || to_clob(B.SQL3) || to_clob(B.SQL4) || to_clob(B.SQL5) || to_clob(B.SQL6) || to_clob(B.SQL7) || to_clob(B.SQL8) || to_clob(B.SQL9),
    B.CLASS_NAME,
    B.METHOD_NAME,
    B.RETURN_TYPE,
    B.REG_DATE,
    B.REG_ID,
    B.MOD_DATE,
    B.MOD_ID,
    B.MAX_KB,
    B.JNDI);'
    from FRM_PRG
    where PROGRAM_ID in {0}", sProgramIDs);

                DataSet ds = new DataSet();
                OracleDataAdapter da = new OracleDataAdapter(sUpdatedListQuery, mOraSourceConn);
                da.Fill(ds, "mytable");

                bRst = SaveScriptFile(ds.Tables["mytable"], sFileName, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                bRst = false;
            }

            return bRst;
        }

        private Boolean saveFrmPrm(String sProgramIDs, String sFileName)
        {
            Boolean bRst = false;

            try
            {
                //Param을 다 지우는 쿼리
                String sDelPrm = String.Format(@"delete from FRM_PRM where PROGRAM_ID in {0};", sProgramIDs);

                String sUpdatedListQuery = String.Format(@"select
    'insert into FRM_PRM(PROGRAM_ID, PARAM_NM, PRM_IDX, IN_OUT)
    values ('''|| PROGRAM_ID ||''','
    ||''''|| PARAM_NM||''','
    ||''''|| PRM_IDX ||''','
    ||''''|| IN_OUT ||''');'
    from FRM_PRM
    where PROGRAM_ID in {0}", sProgramIDs);

                String sEndOfFile = String.Format(@"set define on;
commit;");

                DataSet ds = new DataSet();
                OracleDataAdapter da = new OracleDataAdapter(sUpdatedListQuery, mOraSourceConn);
                da.Fill(ds, "myPrm");

                if (SaveScriptFile(sDelPrm, sFileName, true))
                {
                    if(SaveScriptFile(ds.Tables["myPrm"], sFileName, true))
                        bRst = SaveScriptFile(sEndOfFile, sFileName, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                bRst = false;
            }

            return bRst;
        }

        private Boolean SaveScriptFile(String sText, String sFilePath, Boolean bAppend)
        {
            Boolean bRst = false;

            try
            {

                using (StreamWriter sw = new StreamWriter(sFilePath, bAppend, Encoding.Unicode))
                {
                    sw.WriteLine(sText);
                    sw.Flush();
                    sw.Close();
                }
                bRst = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                bRst = false;
            }
            return bRst;
        }

        private Boolean SaveScriptFile(DataTable dt, String sFilePath, Boolean bAppend)
        {

            Boolean bRst = false;

            try
            {

                int[] maxLengths = new int[dt.Columns.Count];

                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    maxLengths[i] = dt.Columns[i].ColumnName.Length;

                    foreach (DataRow row in dt.Rows)
                    {
                        if (!row.IsNull(i))
                        {
                            int length = row[i].ToString().Length;

                            if (length > maxLengths[i])
                            {
                                maxLengths[i] = length;
                            }
                        }
                    }
                }

                using (StreamWriter sw = new StreamWriter(sFilePath, bAppend, Encoding.Unicode))
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        for (int i = 0; i < dt.Columns.Count; i++)
                        {
                            if (!row.IsNull(i))
                                sw.Write(row[i].ToString().PadRight(maxLengths[i] + 2));
                            else
                                sw.Write(new string(' ', maxLengths[i] + 2));
                        }

                        sw.WriteLine();
                    }
                    sw.Close();
                }
                bRst = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                bRst = false;
            }
            return bRst;
        }

        private void grdData_Click(object sender, EventArgs e)
        {

        }

        private void txtPrgID_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case (char)Keys.Enter:
                    displayProgramID(txtPrgID.Text);
                    break;
                default:
                    break;
            }
        }

        private void richTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (Char)Keys.Tab)
            {
                int currentLine = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);//현재 위치
                int start = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);     //시작위치
                int end = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart + richTextBox1.SelectionLength);//끝위치

                //선택된 라인모두 앞에 공백넣거나 빼기
                string[] lines = richTextBox1.Lines;
                if (ModifierKeys == Keys.Shift)
                {
                    for (int i = start; i <= end; i++)
                    {
                        if (i <= lines.Length)
                            lines[i] = removeLeftTabSpace(lines[i]);
                    }
                }
                else
                {
                    for (int i = start; i <= end; i++)
                    {
                        if (i <= lines.Length)
                            lines[i] = "  " + lines[i];
                    }
                }
                richTextBox1.Lines = lines;

                richTextBox1.SelectionStart = richTextBox1.Find(richTextBox1.Lines[currentLine]);
                richTextBox1.ScrollToCaret();
                e.Handled = true;
            }
        }

        private String removeLeftTabSpace(String str)
        {
            if (str.StartsWith("  "))
                str = str.Remove(0, 2);
            else if (str.StartsWith(" "))
                str = str.Remove(0, 1);

            return str;
        }

        private void grdDataView_DoubleClick(object sender, EventArgs e)
        {
            GridView view = sender as GridView;
            if (view.DataRowCount == 0) return;

            displayProgramID(view.GetFocusedRowCellValue("PROGRAM_ID").ToString());
        }

        private void displayProgramID(String programID)
        {
            clearProgramID();
            if (programID.Length == 0)
            {
                MessageBox.Show("ProgramID가 없습니다.");
                return;
            }
            if (Program.mainForm.mOraSourceConn == null)
            {
                MessageBox.Show("config DB를 Save해주세요.");
                return;
            }

            displayFrmPrg(programID);
            displayFrmPrm(programID);
        }

        private String getTransOperationType(String sType)
        {
            String sRtn = "";

            switch(sType)
            {
                case PRG_TYPE_01:
                    sRtn = "PRG01";
                    break;
                case PRG_TYPE_02:
                    sRtn = "PRG02";
                    break;
                case PRG_TYPE_03:
                    sRtn = "PRG03";
                    break;
                case OP_TYPE_01:
                    sRtn = "OP001";
                    break;
                case OP_TYPE_02:
                    sRtn = "OP002";
                    break;
                case OP_TYPE_03:
                    sRtn = "OP003";
                    break;
                case OP_TYPE_04:
                    sRtn = "OP004";
                    break;
                case OP_TYPE_05:
                    sRtn = "OP005";
                    break;
                case OP_TYPE_06:
                    sRtn = "OP006";
                    break;

                case "PRG01":
                    sRtn = PRG_TYPE_01;
                    break;
                case "PRG02":
                    sRtn = PRG_TYPE_02;
                    break;
                case "PRG03":
                    sRtn = PRG_TYPE_03;
                    break;
                case "OP001":
                    sRtn = OP_TYPE_01;
                    break;
                case "OP002":
                    sRtn = OP_TYPE_02;
                    break;
                case "OP003":
                    sRtn = OP_TYPE_03;
                    break;
                case "OP004":
                    sRtn = OP_TYPE_04;
                    break;
                case "OP005":
                    sRtn = OP_TYPE_05;
                    break;
                case "OP006":
                    sRtn = OP_TYPE_06;
                    break;
            }

            return sRtn;
        }

        private String getReturnByOPType(String sOPType)
        {
            String sRtn = "";

            switch (sOPType)
            {
                case OP_TYPE_01:
                    sRtn = "RT001";
                    break;
                case OP_TYPE_02:
                    sRtn = "RT001";
                    break;
                case OP_TYPE_03:
                    sRtn = "RT002";
                    break;
                case OP_TYPE_04:
                    sRtn = "RT001";
                    break;
                case OP_TYPE_05:
                    sRtn = "RT001";
                    break;
                case OP_TYPE_06:
                    sRtn = "RT001";
                    break;
            }

            return sRtn;
        }

        private void displayFrmPrg(String programID)
        {
            try
            {
                mOraSourceConn.Open();
                DataSet ds = new DataSet();
                OracleDataAdapter da = new OracleDataAdapter(@"select PROGRAM_ID, PRG_TYPE, PRG_NAME, SQL, OP_TYPE, CLASS_NAME, METHOD_NAME, RETURN_TYPE
                    from FRM_PRG where PROGRAM_ID = '" + programID + "'",
                    mOraSourceConn);
                da.Fill(ds, "myPrg");

                DataTable dt = ds.Tables["myPrg"];
                foreach (DataRow dr in dt.Rows)
                {
                    txtPrgID.Text = dr["PROGRAM_ID"].ToString();
                    txtPrgName.Text = dr["PRG_NAME"].ToString();
                    mReturnType = dr["RETURN_TYPE"].ToString();
                    cmbType.Text = getTransOperationType(dr["PRG_TYPE"].ToString());
                    cmbOperation.Text = getTransOperationType(dr["OP_TYPE"].ToString());
                    
                    switch (dr["PRG_TYPE"].ToString())
                    {
                        case "PRG01":
                            richTextBox1.Text = dr["SQL"].ToString();
                            mMaxKB = "4096";
                            break;
                        case "PRG02":
                            richTextBox1.Text = dr["SQL"].ToString();
                            mMaxKB = "4096";
                            break;
                        case "PRG03":
                            richTextBox1.Text = dr["CLASS_NAME"].ToString();
                            break;
                    }
                }
            }
            catch (OracleException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                mOraSourceConn.Close();
            }
        }

        private void displayFrmPrm(String programID)
        {
            try
            {
                mOraSourceConn.Open();
                DataSet ds = new DataSet();
                OracleDataAdapter da = new OracleDataAdapter(@"select PROGRAM_ID, PARAM_NM, PRM_IDX, '' PRM_INPUT from FRM_PRM
                    where PROGRAM_ID = '" + programID + "' order by PRM_IDX",
                    mOraSourceConn);
                da.Fill(ds, "mytable");

                grdPrm.DataSource = ds.Tables["mytable"];
                grdPrmView.BestFitColumns();
            }
            catch (OracleException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                mOraSourceConn.Close();
            }
        }

        private void clearProgramID()
        {
            txtPrgID.Text = "";
            txtPrgName.Text = "";
            cmbType.SelectedIndex = 0;
            cmbOperation.SelectedIndex = 0;
            mReturnType = "";
            mMaxKB = "4096";
    }

        private void btnNew_Click(object sender, EventArgs e)
        {
            GridView view = grdPrmView;
            view.GridControl.Focus();
            view.AddNewRow();

            setReParamIndex();
        }

        private void setReParamIndex()
        {
            for (int i = 0; i < grdPrmView.RowCount; i++)
            {
                grdPrmView.SetRowCellValue(i, "PRM_IDX", (i + 1).ToString());
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            GridView view = grdPrmView;

            view.GridControl.Focus();
            int index = view.FocusedRowHandle;

            view.DeleteSelectedRows();
            setReParamIndex();
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            GridView view = grdPrmView;

            view.GridControl.Focus();

            int index = view.FocusedRowHandle;
            if (index <= 0) return;

            DataRow row1 = view.GetDataRow(index);
            DataRow row2 = view.GetDataRow(index - 1);

            object val1 = row1["PARAM_NM"];
            object val2 = row2["PARAM_NM"];
            row1["PARAM_NM"] = val2;
            row2["PARAM_NM"] = val1;
            view.FocusedRowHandle = index - 1;
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            GridView view = grdPrmView;

            view.GridControl.Focus();

            int index = view.FocusedRowHandle;
            if (index + 1 >= view.RowCount) return;

            DataRow row1 = view.GetDataRow(index);
            DataRow row2 = view.GetDataRow(index + 1);

            object val1 = row1["PARAM_NM"];
            object val2 = row2["PARAM_NM"];
            if (val1 == null || val2 == null)
            {
                MessageBox.Show("Define Parameter First");
                return;
            }
            row1["PARAM_NM"] = val2;
            row2["PARAM_NM"] = val1;
            view.FocusedRowHandle = index + 1;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (mOraSourceConn == null)
            {
                MessageBox.Show("config DB를 Save해주세요.");
                return;
            }

            if (MessageBox.Show("Are you sure to save?", "Save", MessageBoxButtons.YesNo) ==  DialogResult.Yes)
            {
                Cursor.Current = Cursors.WaitCursor;

                if (SaveFrmCode())
                {
                    if (SaveFrmPrg())
                    {
                        if (SaveFrmPrm())
                        {
                            MessageBox.Show("Save Complete");
                        }
                    }
                }

                Cursor.Current = Cursors.Default;
            }
        }

        private Boolean SaveFrmCode()
        {
            Boolean bRst = false;


            try
            {
                mOraSourceConn.Open();
                OracleCommand updateCommand = new OracleCommand();
                updateCommand.Connection = mOraSourceConn;
                updateCommand.CommandText = @"merge into FRM_CODE C
                    using (
                    select
                      'H004' CODE_GBN,
                      :CODE CODE,
                      :CODE_NM CODE_NM,
                      :P_CODE P_CODE,
                      '4' DEPTH
                    from
                      dual) V
                    on(
                    C.CODE = V.CODE)
                    when matched then
                    update set
                       CODE_NM = V.CODE_NM
                    when not matched then
                    insert(
                      CODE_GBN,
                      CODE,
                      CODE_NM,
                      P_CODE,
                      REG_DATE,
                      DEPTH)
                    values(
                      V.CODE_GBN,
                      V.CODE,
                      V.CODE_NM,
                      V.P_CODE,
                      sysdate,
                      V.DEPTH)";
                updateCommand.Parameters.Add("CODE", OracleDbType.Varchar2);
                updateCommand.Parameters.Add("CODE_NM", OracleDbType.Varchar2);
                updateCommand.Parameters.Add("P_CODE", OracleDbType.Varchar2);
                updateCommand.Parameters[0].Value = txtPrgID.Text;
                updateCommand.Parameters[1].Value = txtPrgName.Text;
                updateCommand.Parameters[2].Value = getParentCode(txtPrgID.Text);

                updateCommand.ExecuteNonQuery();
                bRst = true;
            }
            catch (OracleException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                mOraSourceConn.Close();
            }

            return bRst;
        }

        private String getParentCode(String sPrgID)
        {
            String sRtn = "";
            try
            {
                if (sPrgID.IndexOf("-") >= 0)
                {
                    String[] aStr = sPrgID.Split('-');
                    if(aStr.Length > 3)
                    {
                        sRtn = aStr[0] + "-" + aStr[1] + "-" + aStr[2];
                    }
                }
            }
            catch
            {                
            }
            return sRtn;
        }
        
        private Boolean SaveFrmPrg()
        {
            Boolean bRst = false;
            String sPrgType = "";

            try
            {
                sPrgType = getTransOperationType(cmbType.Text);

                mOraSourceConn.Open();

                OracleCommand command = new OracleCommand();
                command.Connection = mOraSourceConn;
                command.CommandText = @"merge into FRM_PRG C
                                        using (select :PROGRAM_ID  PROGRAM_ID,
                                                      :PRG_TYPE    PRG_TYPE,
                                                      :PRG_NAME    PRG_NAME,
                                                      :OP_TYPE     OP_TYPE,
                                                      :SQL         SQL,
                                                      :CLASS_NAME  CLASS_NAME,
                                                      :METHOD_NAME METHOD_NAME,
                                                      :RETURN_TYPE RETURN_TYPE,
                                                      :UPDATE_PSN  UPDATE_PSN,
                                                      :MAX_KB      MAX_KB,
                                                      :JNDI        JNDI
                                                 from DUAL) V
                                        on (C.PROGRAM_ID = V.PROGRAM_ID)
                                        when matched then
                                          update
                                             set PRG_NAME = V.PRG_NAME,
                                                 SQL      = V.SQL,
                                                 OP_TYPE = V.OP_TYPE,
                                                 MOD_DATE = sysdate,
                                                 MOD_ID   = V.UPDATE_PSN
                                        when not matched then
                                          insert
                                            (PROGRAM_ID,
                                             PRG_TYPE,
                                             PRG_NAME,
                                             OP_TYPE,
                                             SQL,
                                             CLASS_NAME,
                                             METHOD_NAME,
                                             RETURN_TYPE,
                                             REG_ID,
                                             MAX_KB,
                                             REG_DATE,
                                             JNDI)
                                          values
                                            (V.PROGRAM_ID,
                                             V.PRG_TYPE,
                                             V.PRG_NAME,
                                             V.OP_TYPE,
                                             V.SQL,
                                             V.CLASS_NAME,
                                             V.METHOD_NAME,
                                             V.RETURN_TYPE,
                                             V.UPDATE_PSN,
                                             V.MAX_KB,
                                             sysdate,
                                             V.JNDI)";
                command.Parameters.Add("PROGRAM_ID", OracleDbType.Varchar2);
                command.Parameters.Add("PRG_TYPE", OracleDbType.Varchar2);
                command.Parameters.Add("PRG_NAME", OracleDbType.Varchar2);
                command.Parameters.Add("OP_TYPE", OracleDbType.Varchar2);
                command.Parameters.Add("SQL", richTextBox1.Text.Length > 4000 ? OracleDbType.Clob : OracleDbType.Varchar2);
                command.Parameters.Add("CLASS_NAME", OracleDbType.Varchar2);
                command.Parameters.Add("METHOD_NAME", OracleDbType.Varchar2);
                command.Parameters.Add("RETURN_TYPE", OracleDbType.Varchar2);
                command.Parameters.Add("UPDATE_PSN", OracleDbType.Varchar2);
                command.Parameters.Add("MAX_KB", OracleDbType.Varchar2);
                command.Parameters.Add("JNDI", OracleDbType.Varchar2);
                command.Parameters[0].Value = txtPrgID.Text;
                command.Parameters[1].Value = sPrgType;
                command.Parameters[2].Value = txtPrgName.Text;
                command.Parameters[3].Value = getTransOperationType(cmbOperation.Text);
                command.Parameters[4].Value = (sPrgType == "PRG01" || sPrgType == "PRG02") ? richTextBox1.Text : "";
                command.Parameters[5].Value = sPrgType == "PRG03" ? richTextBox1.Text : "";
                command.Parameters[6].Value = "";
                command.Parameters[7].Value = getReturnByOPType(cmbOperation.Text);
                command.Parameters[8].Value = mFrameworkUserID;
                command.Parameters[9].Value = mMaxKB;
                command.Parameters[10].Value = sPrgType == "PRG03" ? "" : "hitopsds";
                command.ExecuteNonQuery();
                bRst = true;
            }
            catch (OracleException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                mOraSourceConn.Close();
            }

            return bRst;
        }

        private Boolean SaveFrmPrm()
        {
            Boolean bRst = false;

            try
            {
                mOraSourceConn.Open();

                //1. delete Param
                OracleCommand delCommand = new OracleCommand();
                delCommand.Connection = mOraSourceConn;
                delCommand.CommandText = "delete from FRM_PRM where PROGRAM_ID = '" + txtPrgID.Text + "'";
                delCommand.ExecuteNonQuery();

                //2. Insert Param
                for (int i = 0; i < grdPrmView.RowCount; i++)
                {
                    OracleCommand upCommand = new OracleCommand();
                    upCommand.Connection = mOraSourceConn;
                    upCommand.CommandText = @"insert into FRM_PRM(PROGRAM_ID, PARAM_NM, PRM_IDX, IN_OUT)
                                            values(:PROGRAM_ID, :PARAM_NM, :PRM_IDX, 'IN')";
                    upCommand.Parameters.Add("PROGRAM_ID", OracleDbType.Varchar2);
                    upCommand.Parameters.Add("PARAM_NM", OracleDbType.Varchar2);
                    upCommand.Parameters.Add("PRM_IDX", OracleDbType.Varchar2);
                    upCommand.Parameters[0].Value = txtPrgID.Text;
                    upCommand.Parameters[1].Value = grdPrmView.GetRowCellValue(i, "PARAM_NM");
                    upCommand.Parameters[2].Value = grdPrmView.GetRowCellValue(i, "PRM_IDX");
                    upCommand.ExecuteNonQuery();
                }
                bRst = true;
            }
            catch (OracleException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                mOraSourceConn.Close();
            }

            return bRst;
        }

        private void cmbOperation_SelectedIndexChanged(object sender, EventArgs e)
        {
            mReturnType = getReturnByOPType(cmbOperation.Text);
        }
        #endregion



        #region Make Excel
        private void btnShowTables_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;

            getTableList();
            setEnableScriptButton();

            Cursor.Current = Cursors.Default;
        }

        private void getTableList()
        {
            //List<ProgramList> programIdList = new List<ProgramList>();

            if (mOraSourceConn == null)
                return;

            try
            {
                String sUpdatedListQuery = String.Format(@"select T.TABLE_NAME, T.TABLESPACE_NAME, C.COMMENTS
from USER_TABLES T, USER_TAB_COMMENTS C
where
  T.TABLE_NAME = C.TABLE_NAME
order by T.TABLE_NAME", mFrameworkUserID);

                mOraSourceConn.Open();
                DataSet ds = new DataSet();
                OracleDataAdapter da = new OracleDataAdapter(sUpdatedListQuery, mOraSourceConn);
                da.Fill(ds, "tableList");
                gridTable.DataSource = ds.Tables["tableList"];
                gridTableView.BestFitColumns();
            }
            catch (OracleException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                mOraSourceConn.Close();
            }
        }

        private void btnCreateTableExcel_Click(object sender, EventArgs e)
        {
            
        }

        private void getTableDetails()
        {
            String sTableName = "";
            String sUpdatedListQuery = String.Format(@"select
  a.COLUMN_NAME,
  COMMENTS,
  DATA_TYPE,
  --data_length,
  case when DATA_TYPE = 'DATE' then null when DATA_SCALE is null then DATA_LENGTH else DATA_PRECISION + DATA_SCALE/10 end DATA_LENGTH,
  DATA_DEFAULT,
  (select distinct DECODE(SA.CONSTRAINT_TYPE,'P','Y', '' ) PK
  from ALL_CONSTRAINTS SA,
    ALL_CONS_COLUMNS SB
  where SA.CONSTRAINT_NAME = SB.CONSTRAINT_NAME
    and sa.constraint_type = 'P'
    and SA.TABLE_NAME = a.TABLE_NAME
    and SB.COLUMN_NAME = a.COLUMN_NAME
  ) PK,
  NULLABLE,
  (select distinct DECODE(SA.CONSTRAINT_TYPE,'R','Y', '' ) PK
  from ALL_CONSTRAINTS SA,
    ALL_CONS_COLUMNS SB
  where SA.CONSTRAINT_NAME = SB.CONSTRAINT_NAME
    and SA.CONSTRAINT_TYPE = 'Y'
    and SA.TABLE_NAME = a.TABLE_NAME
    and SB.COLUMN_NAME = a.COLUMN_NAME
  ) FK
from USER_TAB_COLUMNS a,
  USER_COL_COMMENTS B
where a.TABLE_NAME = {0}
  and a.TABLE_NAME = B.TABLE_NAME
  and a.COLUMN_NAME = B.COLUMN_NAME
order by COLUMN_ID;", sTableName);
        }
        #endregion

    }
}
