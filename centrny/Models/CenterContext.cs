using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace centrny.Models;

public partial class CenterContext : DbContext
{
    public CenterContext()
    {
    }

    public CenterContext(DbContextOptions<CenterContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Answer> Answers { get; set; }

    public virtual DbSet<Attend> Attends { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<Center> Centers { get; set; }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<EduYear> EduYears { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<Exam> Exams { get; set; }

    public virtual DbSet<ExamQuestion> ExamQuestions { get; set; }

    public virtual DbSet<Expense> Expenses { get; set; }

    public virtual DbSet<Group> Groups { get; set; }

    public virtual DbSet<GroupPage> GroupPages { get; set; }

    public virtual DbSet<Hall> Halls { get; set; }

    public virtual DbSet<Item> Items { get; set; }

    public virtual DbSet<ItemType> ItemTypes { get; set; }

    public virtual DbSet<Learn> Learns { get; set; }

    public virtual DbSet<Lesson> Lessons { get; set; }

    public virtual DbSet<Level> Levels { get; set; }

    public virtual DbSet<Lockup> Lockups { get; set; }

    public virtual DbSet<Module> Modules { get; set; }

    public virtual DbSet<Page> Pages { get; set; }

    public virtual DbSet<PlanSubject> PlanSubjects { get; set; }

    public virtual DbSet<PositionType> PositionTypes { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<Reservation> Reservations { get; set; }

    public virtual DbSet<Root> Roots { get; set; }

    public virtual DbSet<RootModule> RootModules { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<StudentAnswer> StudentAnswers { get; set; }

    public virtual DbSet<StudentExam> StudentExams { get; set; }

    public virtual DbSet<StudentPlan> StudentPlans { get; set; }

    public virtual DbSet<StudentSubjectPlan> StudentSubjectPlans { get; set; }

    public virtual DbSet<Subject> Subjects { get; set; }

    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

    public virtual DbSet<Teach> Teaches { get; set; }

    public virtual DbSet<Teacher> Teachers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<WalletExam> WalletExams { get; set; }

    public virtual DbSet<Year> Years { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=center.cfqynilckjf8.us-east-2.rds.amazonaws.com,1433;Database=Center;User Id=admin;Password=hamody123;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasKey(e => e.AnswerCode);

            entity.ToTable("Answer");

            entity.Property(e => e.AnswerCode).HasColumnName("Answer_Code");
            entity.Property(e => e.AnswerContent)
                .HasMaxLength(500)
                .HasColumnName("Answer_Content");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.IsTrue).HasColumnName("isTrue");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.QuestionCode).HasColumnName("Question_Code");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.AnswerInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Answer_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.AnswerLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Answer_User1");

            entity.HasOne(d => d.QuestionCodeNavigation).WithMany(p => p.Answers)
                .HasForeignKey(d => d.QuestionCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Answer_Question");
        });

        modelBuilder.Entity<Attend>(entity =>
        {
            entity.HasKey(e => new { e.TeacherCode, e.ScheduleCode, e.ClassId, e.HallId, e.StudentId }).HasName("PK_Attend_1");

            entity.ToTable("Attend", tb =>
                {
                    tb.HasTrigger("AttendTrigger");
                    tb.HasTrigger("Trigger_After_Insert_Update_Total_Amount");
                    tb.HasTrigger("Trigger_UpdateRemainingCount");
                    tb.HasTrigger("Trigger_UpdateTotalAmount");
                    tb.HasTrigger("UpdateAttendDetails");
                    tb.HasTrigger("trg_Set_SessionPrice_Zero_On_Type2");
                    tb.HasTrigger("trg_Update_Attend_SessionPrice");
                });

            entity.Property(e => e.TeacherCode).HasColumnName("Teacher_Code");
            entity.Property(e => e.ScheduleCode).HasColumnName("Schedule_Code");
            entity.Property(e => e.ClassId).HasColumnName("Class_Id");
            entity.Property(e => e.HallId).HasColumnName("Hall_Id");
            entity.Property(e => e.StudentId).HasColumnName("Student_Id");
            entity.Property(e => e.AttendDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Attend_Date");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.SessionPrice)
                .HasColumnType("money")
                .HasColumnName("Session_Price");
            entity.Property(e => e.Type).HasDefaultValue(1);

            entity.HasOne(d => d.Class).WithMany(p => p.Attends)
                .HasForeignKey(d => d.ClassId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attend_Class");

            entity.HasOne(d => d.Hall).WithMany(p => p.Attends)
                .HasForeignKey(d => d.HallId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attend_Hall");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Attends)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attend_Root");

            entity.HasOne(d => d.ScheduleCodeNavigation).WithMany(p => p.Attends)
                .HasForeignKey(d => d.ScheduleCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attend_Schedule");

            entity.HasOne(d => d.Student).WithMany(p => p.Attends)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attend_Student");

            entity.HasOne(d => d.TeacherCodeNavigation).WithMany(p => p.Attends)
                .HasForeignKey(d => d.TeacherCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attend_Teacher");

            entity.HasOne(d => d.TypeNavigation).WithMany(p => p.Attends)
                .HasForeignKey(d => d.Type)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attend_Lockup");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchCode);

            entity.ToTable("Branch");

            entity.Property(e => e.BranchCode).HasColumnName("Branch_Code");
            entity.Property(e => e.Address)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.BranchName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Branch_Name");
            entity.Property(e => e.CenterCode).HasColumnName("Center_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.StartTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("Start_Time");

            entity.HasOne(d => d.CenterCodeNavigation).WithMany(p => p.Branches)
                .HasForeignKey(d => d.CenterCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Branch_Center");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.BranchInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Branch_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.BranchLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Branch_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Branches)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Branch_Branch");
        });

        modelBuilder.Entity<Center>(entity =>
        {
            entity.HasKey(e => e.CenterCode);

            entity.ToTable("Center");

            entity.Property(e => e.CenterCode).HasColumnName("Center_Code");
            entity.Property(e => e.CenterAddress)
                .HasMaxLength(100)
                .HasColumnName("Center_Address");
            entity.Property(e => e.CenterName)
                .HasMaxLength(100)
                .HasColumnName("Center_Name");
            entity.Property(e => e.CenterPhone)
                .HasMaxLength(50)
                .HasColumnName("Center_Phone");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.LastInsertTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Insert_Time");
            entity.Property(e => e.LastInsertUser).HasColumnName("Last_Insert_User");
            entity.Property(e => e.OwnerName)
                .HasMaxLength(100)
                .HasColumnName("Owner_Name");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.CenterInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Center_User");

            entity.HasOne(d => d.LastInsertUserNavigation).WithMany(p => p.CenterLastInsertUserNavigations)
                .HasForeignKey(d => d.LastInsertUser)
                .HasConstraintName("FK_Center_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Centers)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Center_Root");
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.ClassCode);

            entity.ToTable("Class", tb => tb.HasTrigger("Trigger_After_Insert_Update_Center_Amount"));

            entity.Property(e => e.ClassCode).HasColumnName("Class_Code");
            entity.Property(e => e.BranchCode).HasColumnName("Branch_Code");
            entity.Property(e => e.CenterAmount)
                .HasColumnType("money")
                .HasColumnName("Center_Amount");
            entity.Property(e => e.ClassName)
                .HasMaxLength(50)
                .HasColumnName("Class_Name");
            entity.Property(e => e.EduYearCode).HasColumnName("Edu_Year_Code");
            entity.Property(e => e.HallCode).HasColumnName("Hall_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.NoOfStudents).HasColumnName("No_Of_Students");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.ScheduleCode).HasColumnName("Schedule_Code");
            entity.Property(e => e.SubjectCode).HasColumnName("Subject_Code");
            entity.Property(e => e.TeacherAmount)
                .HasColumnType("money")
                .HasColumnName("Teacher_Amount");
            entity.Property(e => e.TeacherCode).HasColumnName("Teacher_Code");
            entity.Property(e => e.TeacherSubAmount)
                .HasColumnType("money")
                .HasColumnName("Teacher_Sub_Amount");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("money")
                .HasColumnName("Total_Amount");
            entity.Property(e => e.YearCode).HasColumnName("Year_Code");

            entity.HasOne(d => d.BranchCodeNavigation).WithMany(p => p.Classes)
                .HasForeignKey(d => d.BranchCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Class_Branch");

            entity.HasOne(d => d.EduYearCodeNavigation).WithMany(p => p.Classes)
                .HasForeignKey(d => d.EduYearCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Class_Edu_Year");

            entity.HasOne(d => d.HallCodeNavigation).WithMany(p => p.Classes)
                .HasForeignKey(d => d.HallCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Class_Hall");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.ClassInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Class_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.ClassLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Class_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Classes)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Class_Root");

            entity.HasOne(d => d.ScheduleCodeNavigation).WithMany(p => p.Classes)
                .HasForeignKey(d => d.ScheduleCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Class_Schedule");

            entity.HasOne(d => d.SubjectCodeNavigation).WithMany(p => p.Classes)
                .HasForeignKey(d => d.SubjectCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Class_Subject");

            entity.HasOne(d => d.TeacherCodeNavigation).WithMany(p => p.Classes)
                .HasForeignKey(d => d.TeacherCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Class_Teacher");

            entity.HasOne(d => d.YearCodeNavigation).WithMany(p => p.Classes)
                .HasForeignKey(d => d.YearCode)
                .HasConstraintName("FK_Class_Year");
        });

        modelBuilder.Entity<EduYear>(entity =>
        {
            entity.HasKey(e => e.EduCode);

            entity.ToTable("Edu_Year");

            entity.Property(e => e.EduCode).HasColumnName("Edu_Code");
            entity.Property(e => e.EduName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Edu_Name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.YearCode).HasColumnName("Year_Code");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.EduYears)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Edu_Year_Root");

            entity.HasOne(d => d.YearCodeNavigation).WithMany(p => p.EduYears)
                .HasForeignKey(d => d.YearCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Edu_Year_Year");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmployeeCode);

            entity.ToTable("Employee");

            entity.Property(e => e.EmployeeCode).HasColumnName("Employee_Code");
            entity.Property(e => e.BranchCode).HasColumnName("Branch_Code");
            entity.Property(e => e.EmploteePositionCode).HasColumnName("Emplotee_Position_code");
            entity.Property(e => e.EmployeeEmail)
                .HasMaxLength(100)
                .HasColumnName("Employee_Email");
            entity.Property(e => e.EmployeeName)
                .HasMaxLength(50)
                .HasColumnName("Employee_Name");
            entity.Property(e => e.EmployeePhone)
                .HasMaxLength(50)
                .HasColumnName("Employee_Phone");
            entity.Property(e => e.EmployeeSalary)
                .HasColumnType("money")
                .HasColumnName("Employee_salary");
            entity.Property(e => e.EmployeeStartDate).HasColumnName("Employee_Start_Date");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.LastUpdatTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Updat_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.UserCode).HasColumnName("User_Code");

            entity.HasOne(d => d.BranchCodeNavigation).WithMany(p => p.Employees)
                .HasForeignKey(d => d.BranchCode)
                .HasConstraintName("FK_Employee_Branch");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.EmployeeInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Employee_User1");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.EmployeeLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Employee_User2");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Employees)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Employee_Root");

            entity.HasOne(d => d.UserCodeNavigation).WithMany(p => p.EmployeeUserCodeNavigations)
                .HasForeignKey(d => d.UserCode)
                .HasConstraintName("FK_Employee_User");
        });

        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasKey(e => e.ExamCode);

            entity.ToTable("Exam", tb => tb.HasTrigger("CalculateExamPercentage"));

            entity.Property(e => e.ExamCode).HasColumnName("Exam_Code");
            entity.Property(e => e.BranchCode).HasColumnName("Branch_Code");
            entity.Property(e => e.EduYearCode).HasColumnName("Edu_Year_Code");
            entity.Property(e => e.ExamDegree)
                .HasMaxLength(50)
                .HasColumnName("Exam_Degree");
            entity.Property(e => e.ExamName)
                .HasMaxLength(100)
                .HasColumnName("Exam_Name");
            entity.Property(e => e.ExamPercentage)
                .HasMaxLength(10)
                .HasColumnName("Exam_Percentage");
            entity.Property(e => e.ExamResult)
                .HasMaxLength(50)
                .HasColumnName("Exam_Result");
            entity.Property(e => e.ExamTimer).HasColumnName("Exam_timer");
            entity.Property(e => e.InserTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Inser_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsDone).HasColumnName("isDone");
            entity.Property(e => e.IsExam).HasColumnName("isExam");
            entity.Property(e => e.IsOnline).HasColumnName("isOnline");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.SubjectCode).HasColumnName("Subject_Code");
            entity.Property(e => e.TeacherCode).HasColumnName("Teacher_Code");

            entity.HasOne(d => d.BranchCodeNavigation).WithMany(p => p.Exams)
                .HasForeignKey(d => d.BranchCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exam_Branch");

            entity.HasOne(d => d.EduYearCodeNavigation).WithMany(p => p.Exams)
                .HasForeignKey(d => d.EduYearCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exam_Edu_Year");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.ExamInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exam_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.ExamLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Exam_User1");

            entity.HasOne(d => d.SubjectCodeNavigation).WithMany(p => p.Exams)
                .HasForeignKey(d => d.SubjectCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exam_Subject");

            entity.HasOne(d => d.TeacherCodeNavigation).WithMany(p => p.Exams)
                .HasForeignKey(d => d.TeacherCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exam_Teacher");
        });

        modelBuilder.Entity<ExamQuestion>(entity =>
        {
            entity.HasKey(e => new { e.ExamCode, e.QuestionCode });

            entity.ToTable("Exam_Questions", tb =>
                {
                    tb.HasTrigger("SetExamDegree");
                    tb.HasTrigger("SyncStudentAnswerDegree");
                });

            entity.Property(e => e.ExamCode).HasColumnName("Exam_Code");
            entity.Property(e => e.QuestionCode).HasColumnName("Question_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.LastUpdateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.QuestionDegree).HasColumnName("Question_Degree");

            entity.HasOne(d => d.ExamCodeNavigation).WithMany(p => p.ExamQuestions)
                .HasForeignKey(d => d.ExamCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exam_Questions_Exam");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.ExamQuestionInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exam_Questions_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.ExamQuestionLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Exam_Questions_User1");

            entity.HasOne(d => d.QuestionCodeNavigation).WithMany(p => p.ExamQuestions)
                .HasForeignKey(d => d.QuestionCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Exam_Questions_Question");
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.ExpensesCode);

            entity.Property(e => e.ExpensesCode).HasColumnName("Expenses_Code");
            entity.Property(e => e.EmployeeCode).HasColumnName("Employee_Code");
            entity.Property(e => e.ExpenseTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("Expense_Time");
            entity.Property(e => e.ExpensesAmount)
                .HasColumnType("money")
                .HasColumnName("Expenses_Amount");
            entity.Property(e => e.ExpensesReason)
                .HasMaxLength(100)
                .HasColumnName("Expenses_Reason");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.LastUpdateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");

            entity.HasOne(d => d.EmployeeCodeNavigation).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.EmployeeCode)
                .HasConstraintName("FK_Expenses_Employee");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.ExpenseInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Expenses_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.ExpenseLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Expenses_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Expenses_Root");
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.GroupCode).HasName("PK_Table_1");

            entity.ToTable("Group");

            entity.Property(e => e.GroupCode).HasColumnName("Group_Code");
            entity.Property(e => e.GroupDesc)
                .HasMaxLength(400)
                .HasColumnName("Group_Desc");
            entity.Property(e => e.GroupName)
                .HasMaxLength(50)
                .HasColumnName("Group_Name");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.GroupInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Group_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.GroupLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Group_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Groups)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Group_Root");
        });

        modelBuilder.Entity<GroupPage>(entity =>
        {
            entity.HasKey(e => new { e.GroupCode, e.PageCode });

            entity.ToTable("Group_Page");

            entity.Property(e => e.GroupCode).HasColumnName("Group_Code");
            entity.Property(e => e.PageCode).HasColumnName("Page_Code");
            entity.Property(e => e.DeleteFlag)
                .HasDefaultValue(true)
                .HasColumnName("Delete_Flag");
            entity.Property(e => e.InsertFlag)
                .HasDefaultValue(true)
                .HasColumnName("Insert_Flag");
            entity.Property(e => e.UpdateFlag)
                .HasDefaultValue(true)
                .HasColumnName("Update_Flag");

            entity.HasOne(d => d.GroupCodeNavigation).WithMany(p => p.GroupPages)
                .HasForeignKey(d => d.GroupCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Group_Page_Group");

            entity.HasOne(d => d.PageCodeNavigation).WithMany(p => p.GroupPages)
                .HasForeignKey(d => d.PageCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Group_Page_Page");
        });

        modelBuilder.Entity<Hall>(entity =>
        {
            entity.HasKey(e => e.HallCode);

            entity.ToTable("Hall");

            entity.Property(e => e.HallCode).HasColumnName("Hall_code");
            entity.Property(e => e.BranchCode).HasColumnName("Branch_Code");
            entity.Property(e => e.HallCapacity).HasColumnName("Hall_Capacity");
            entity.Property(e => e.HallName)
                .HasMaxLength(100)
                .HasColumnName("Hall_Name");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");

            entity.HasOne(d => d.BranchCodeNavigation).WithMany(p => p.Halls)
                .HasForeignKey(d => d.BranchCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Hall_Branch");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.HallInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Hall_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.HallLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Hall_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Halls)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Hall_Root");
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(e => e.ItemCode);

            entity.ToTable("Item", tb => tb.HasTrigger("EncryptItemKey"));

            entity.Property(e => e.ItemCode)
                .ValueGeneratedNever()
                .HasColumnName("Item_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.ItemKey)
                .HasMaxLength(50)
                .HasColumnName("Item_Key");
            entity.Property(e => e.ItemTypeKey).HasColumnName("Item_Type_Key");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.StudentCode).HasColumnName("Student_Code");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.ItemInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Item_User");

            entity.HasOne(d => d.ItemTypeKeyNavigation).WithMany(p => p.Items)
                .HasForeignKey(d => d.ItemTypeKey)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Item_ItemType");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.ItemLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Item_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Items)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Item_Root");

            entity.HasOne(d => d.StudentCodeNavigation).WithMany(p => p.Items)
                .HasForeignKey(d => d.StudentCode)
                .HasConstraintName("FK_Item_Student");
        });

        modelBuilder.Entity<ItemType>(entity =>
        {
            entity.HasKey(e => e.ItemTypeCode).HasName("PK_Table_1_1");

            entity.ToTable("ItemType");

            entity.Property(e => e.ItemTypeCode).HasColumnName("Item_type_Code");
            entity.Property(e => e.ItemTypeName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Item_type_name");
        });

        modelBuilder.Entity<Learn>(entity =>
        {
            entity.HasKey(e => new { e.SubjectCode, e.TeacherCode, e.EduYearCode, e.BranchCode, e.RootCode, e.StudentCode });

            entity.ToTable("Learn");

            entity.Property(e => e.SubjectCode).HasColumnName("Subject_Code");
            entity.Property(e => e.TeacherCode).HasColumnName("Teacher_Code");
            entity.Property(e => e.EduYearCode).HasColumnName("Edu_Year_Code");
            entity.Property(e => e.BranchCode).HasColumnName("Branch_Code");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.StudentCode).HasColumnName("Student_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.IsOnline).HasColumnName("isOnline");
            entity.Property(e => e.LastUpdateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.StudentFee).HasColumnName("Student_Fee");

            entity.HasOne(d => d.BranchCodeNavigation).WithMany(p => p.Learns)
                .HasForeignKey(d => d.BranchCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Learn_Branch");

            entity.HasOne(d => d.EduYearCodeNavigation).WithMany(p => p.Learns)
                .HasForeignKey(d => d.EduYearCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Learn_Edu_Year");

            entity.HasOne(d => d.SubjectCodeNavigation).WithMany(p => p.Learns)
                .HasForeignKey(d => d.SubjectCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Learn_Subject");
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasKey(e => e.LessonCode);

            entity.ToTable("Lesson");

            entity.Property(e => e.LessonCode).HasColumnName("Lesson_Code");
            entity.Property(e => e.EduYearCode).HasColumnName("Edu_Year_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.LastUpdatTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Updat_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.LessonName)
                .HasMaxLength(100)
                .HasColumnName("Lesson_Name");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.SubjectCode).HasColumnName("Subject_Code");
            entity.Property(e => e.TeacherCode).HasColumnName("Teacher_Code");

            entity.HasOne(d => d.EduYearCodeNavigation).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.EduYearCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lesson_Edu_Year");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.LessonInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lesson_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.LessonLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lesson_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lesson_Root");

            entity.HasOne(d => d.SubjectCodeNavigation).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.SubjectCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lesson_Subject");

            entity.HasOne(d => d.TeacherCodeNavigation).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.TeacherCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Lesson_Teacher");
        });

        modelBuilder.Entity<Level>(entity =>
        {
            entity.HasKey(e => e.LevelCode);

            entity.ToTable("Level");

            entity.Property(e => e.LevelCode).HasColumnName("Level_code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.LevelName)
                .HasMaxLength(100)
                .HasColumnName("Level_Name");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.LevelInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Level_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.LevelLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Level_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Levels)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Level_Root");
        });

        modelBuilder.Entity<Lockup>(entity =>
        {
            entity.HasKey(e => e.PaymentCode);

            entity.ToTable("Lockup");

            entity.Property(e => e.PaymentCode).HasColumnName("Payment_Code");
            entity.Property(e => e.PaymentName)
                .HasMaxLength(50)
                .HasColumnName("Payment_Name");
        });

        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasKey(e => e.ModuleCode);

            entity.ToTable("Module");

            entity.Property(e => e.ModuleCode).HasColumnName("Module_code");
            entity.Property(e => e.ModuleName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Module_name");
        });

        modelBuilder.Entity<Page>(entity =>
        {
            entity.HasKey(e => e.PageCode);

            entity.ToTable("Page");

            entity.Property(e => e.PageCode)
                .ValueGeneratedNever()
                .HasColumnName("Page_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsParent).HasColumnName("isParent");
            entity.Property(e => e.LastUpdateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.ModuleCode).HasColumnName("Module_Code");
            entity.Property(e => e.PageName)
                .HasMaxLength(50)
                .HasColumnName("Page_Name");
            entity.Property(e => e.PageParent).HasColumnName("Page_Parent");
            entity.Property(e => e.PagePath)
                .HasMaxLength(50)
                .HasColumnName("Page_Path");
            entity.Property(e => e.PageSort).HasColumnName("Page_Sort");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.PageInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Page_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.PageLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Page_User1");

            entity.HasOne(d => d.ModuleCodeNavigation).WithMany(p => p.Pages)
                .HasForeignKey(d => d.ModuleCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Page_Module");
        });

        modelBuilder.Entity<PlanSubject>(entity =>
        {
            entity.HasKey(e => new { e.PlanCode, e.SubjectCode });

            entity.ToTable("Plan_Subject", tb => tb.HasTrigger("Plan_Total_Count"));

            entity.Property(e => e.PlanCode)
                .ValueGeneratedOnAdd()
                .HasColumnName("Plan_Code");
            entity.Property(e => e.SubjectCode).HasColumnName("Subject_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.LastInsertTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Insert_Time");
            entity.Property(e => e.LastInsertUser).HasColumnName("Last_Insert_User");
            entity.Property(e => e.SubscribtionPlanCode).HasColumnName("Subscribtion_Plan_Code");
            entity.Property(e => e.YearCode).HasColumnName("Year_Code");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.PlanSubjectInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Plan_Subject_User");

            entity.HasOne(d => d.LastInsertUserNavigation).WithMany(p => p.PlanSubjectLastInsertUserNavigations)
                .HasForeignKey(d => d.LastInsertUser)
                .HasConstraintName("FK_Plan_Subject_User1");

            entity.HasOne(d => d.SubjectCodeNavigation).WithMany(p => p.PlanSubjects)
                .HasForeignKey(d => d.SubjectCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Plan_Subject_Subject");

            entity.HasOne(d => d.SubscribtionPlanCodeNavigation).WithMany(p => p.PlanSubjects)
                .HasForeignKey(d => d.SubscribtionPlanCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Plan_Subject_Subscription_Plan");

            entity.HasOne(d => d.YearCodeNavigation).WithMany(p => p.PlanSubjects)
                .HasForeignKey(d => d.YearCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Plan_Subject_Year");
        });

        modelBuilder.Entity<PositionType>(entity =>
        {
            entity.HasKey(e => e.PositionCode);

            entity.ToTable("Position_Type");

            entity.Property(e => e.PositionCode).HasColumnName("Position_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.LastUpdateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.PositionName)
                .HasMaxLength(50)
                .HasColumnName("Position_Name");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.PositionTypeInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Position_Type_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.PositionTypeLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Position_Type_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.PositionTypes)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Position_Type_Root");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.QuestionCode);

            entity.ToTable("Question");

            entity.Property(e => e.QuestionCode).HasColumnName("Question_Code");
            entity.Property(e => e.ExamCode).HasColumnName("Exam_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.QuestionContent)
                .HasMaxLength(500)
                .HasColumnName("Question_Content");

            entity.HasOne(d => d.ExamCodeNavigation).WithMany(p => p.Questions)
                .HasForeignKey(d => d.ExamCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Question_Exam");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.QuestionInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Question_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.QuestionLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Question_User1");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(e => e.ReservationCode);

            entity.ToTable("Reservation", tb => tb.HasTrigger("check_schedule_conflicts"));

            entity.Property(e => e.ReservationCode).HasColumnName("Reservation_Code");
            entity.Property(e => e.Cost).HasColumnType("money");
            entity.Property(e => e.Description)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.FinalCost).HasColumnName("Final_Cost");
            entity.Property(e => e.HallCode).HasColumnName("Hall_Code");
            entity.Property(e => e.RTime)
                .HasColumnType("datetime")
                .HasColumnName("R_time");
            entity.Property(e => e.TeacherCode).HasColumnName("Teacher_Code");

            entity.HasOne(d => d.HallCodeNavigation).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.HallCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reservation_Hall");

            entity.HasOne(d => d.TeacherCodeNavigation).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.TeacherCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reservation_Teacher");
        });

        modelBuilder.Entity<Root>(entity =>
        {
            entity.HasKey(e => e.RootCode);

            entity.ToTable("Root", tb => tb.HasTrigger("InsertRootData"));

            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.IsCenter).HasColumnName("isCenter");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.NoOfCenter)
                .HasDefaultValue(1)
                .HasColumnName("No_Of_Center");
            entity.Property(e => e.NoOfUser)
                .HasDefaultValue(1)
                .HasColumnName("No_Of_User");
            entity.Property(e => e.RootAddress)
                .HasMaxLength(100)
                .HasColumnName("Root_Address");
            entity.Property(e => e.RootEmail)
                .HasMaxLength(50)
                .HasColumnName("Root_Email");
            entity.Property(e => e.RootFees)
                .HasColumnType("money")
                .HasColumnName("Root_Fees");
            entity.Property(e => e.RootName)
                .HasMaxLength(100)
                .HasColumnName("Root_Name");
            entity.Property(e => e.RootOwner)
                .HasMaxLength(100)
                .HasColumnName("Root_Owner");
            entity.Property(e => e.RootPhone)
                .HasMaxLength(50)
                .HasColumnName("Root_Phone");
            entity.Property(e => e.StartTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Start_Time");
        });

        modelBuilder.Entity<RootModule>(entity =>
        {
            entity.HasKey(e => new { e.RootCode, e.ModuleCode });

            entity.ToTable("Root_Modules", tb =>
                {
                    tb.HasTrigger("trg_AfterInsert_RootModules");
                    tb.HasTrigger("trg_UpdateRootModuleName");
                });

            entity.HasIndex(e => new { e.RootCode, e.ModuleCode }, "IX_Root_Modules").IsUnique();

            entity.Property(e => e.RootCode).HasColumnName("Root_code");
            entity.Property(e => e.ModuleCode).HasColumnName("Module_code");
            entity.Property(e => e.ModuleName)
                .HasMaxLength(50)
                .HasColumnName("Module_Name");

            entity.HasOne(d => d.ModuleCodeNavigation).WithMany(p => p.RootModules)
                .HasForeignKey(d => d.ModuleCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Root_Modules_Module");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.RootModules)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Root_Modules_Root");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleCode);

            entity.ToTable("Schedule");

            entity.Property(e => e.ScheduleCode).HasColumnName("Schedule_Code");
            entity.Property(e => e.DayOfWeek)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.EduYearCode).HasColumnName("Edu_Year_Code");
            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.HallCode).HasColumnName("Hall_Code");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.ScheduleAmount)
                .HasColumnType("money")
                .HasColumnName("Schedule_Amount");
            entity.Property(e => e.ScheduleName)
                .HasMaxLength(100)
                .HasColumnName("Schedule_Name");
            entity.Property(e => e.StartTime).HasColumnType("datetime");
            entity.Property(e => e.SubjectCode).HasColumnName("Subject_code");
            entity.Property(e => e.TeacherCode).HasColumnName("Teacher_Code");

            entity.HasOne(d => d.EduYearCodeNavigation).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.EduYearCode)
                .HasConstraintName("FK_Schedule_Edu_Year");

            entity.HasOne(d => d.HallCodeNavigation).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.HallCode)
                .HasConstraintName("FK_Schedule_Hall");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.RootCode)
                .HasConstraintName("FK_Schedule_Root");

            entity.HasOne(d => d.SubjectCodeNavigation).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.SubjectCode)
                .HasConstraintName("FK_Schedule_Subject");

            entity.HasOne(d => d.TeacherCodeNavigation).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.TeacherCode)
                .HasConstraintName("FK_Schedule_Teacher");
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.StudentCode);

            entity.ToTable("Student");

            entity.Property(e => e.StudentCode).HasColumnName("Student_Code");
            entity.Property(e => e.BranchCode).HasColumnName("Branch_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_user");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.LastInsertTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_insert_Time");
            entity.Property(e => e.LastInsertUser).HasColumnName("Last_Insert_User");
            entity.Property(e => e.RootCode)
                .HasDefaultValue(1)
                .HasColumnName("Root_Code");
            entity.Property(e => e.StudentBirthdate).HasColumnName("Student_Birthdate");
            entity.Property(e => e.StudentGender).HasColumnName("Student_Gender");
            entity.Property(e => e.StudentName)
                .HasMaxLength(100)
                .HasColumnName("Student_Name");
            entity.Property(e => e.StudentParentPhone)
                .HasMaxLength(50)
                .HasColumnName("Student_Parent_Phone");
            entity.Property(e => e.StudentPhone)
                .HasMaxLength(50)
                .HasColumnName("Student_Phone");
            entity.Property(e => e.SubscribtionTime).HasColumnName("Subscribtion_Time");
            entity.Property(e => e.YearCode).HasColumnName("Year_code");

            entity.HasOne(d => d.BranchCodeNavigation).WithMany(p => p.Students)
                .HasForeignKey(d => d.BranchCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Branch");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.StudentInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_User");

            entity.HasOne(d => d.LastInsertUserNavigation).WithMany(p => p.StudentLastInsertUserNavigations)
                .HasForeignKey(d => d.LastInsertUser)
                .HasConstraintName("FK_Student_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Students)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Root");

            entity.HasOne(d => d.YearCodeNavigation).WithMany(p => p.Students)
                .HasForeignKey(d => d.YearCode)
                .HasConstraintName("FK_Student_Year");
        });

        modelBuilder.Entity<StudentAnswer>(entity =>
        {
            entity.HasKey(e => new { e.StudentCode, e.ExamCode, e.QuestionCode });

            entity.ToTable("Student_Answers", tb =>
                {
                    tb.HasTrigger("InsertSumToStudentExam");
                    tb.HasTrigger("UnifiedTrigger");
                });

            entity.Property(e => e.StudentCode).HasColumnName("Student_Code");
            entity.Property(e => e.ExamCode).HasColumnName("Exam_Code");
            entity.Property(e => e.QuestionCode).HasColumnName("Question_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.QuestionDegree).HasColumnName("Question_Degree");
            entity.Property(e => e.RightAnswerCode).HasColumnName("Right_Answer_Code");
            entity.Property(e => e.StudentAnswerCode).HasColumnName("Student_Answer_Code");
            entity.Property(e => e.StudentDegree).HasColumnName("Student_Degree");
        });

        modelBuilder.Entity<StudentExam>(entity =>
        {
            entity.HasKey(e => new { e.StudentCode, e.ExamCode });

            entity.ToTable("Student_Exam", tb =>
                {
                    tb.HasTrigger("CalculateStudentPercentage");
                    tb.HasTrigger("DecreaseWalletCount");
                    tb.HasTrigger("InsertQuestionsToStudentAnswers");
                });

            entity.Property(e => e.StudentCode).HasColumnName("Student_Code");
            entity.Property(e => e.ExamCode).HasColumnName("Exam_Code");
            entity.Property(e => e.ExamDegree).HasColumnName("Exam_Degree");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.LastUpdateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.StudentPercentage).HasColumnName("Student_Percentage");
            entity.Property(e => e.StudentResult).HasColumnName("Student_Result");
        });

        modelBuilder.Entity<StudentPlan>(entity =>
        {
            entity.HasKey(e => new { e.SubscriptionPlanCode, e.StudentCode, e.EduYearCode }).HasName("PK_Student_Plan_1");

            entity.ToTable("Student_Plan", tb =>
                {
                    tb.HasTrigger("Student_Subject_Plan_Total");
                    tb.HasTrigger("mark_expired");
                });

            entity.Property(e => e.SubscriptionPlanCode).HasColumnName("Subscription_Plan_Code");
            entity.Property(e => e.StudentCode).HasColumnName("Student_Code");
            entity.Property(e => e.EduYearCode).HasColumnName("Edu_Year_Code");
            entity.Property(e => e.ExpiryDate).HasColumnName("Expiry_Date");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.IsExpired)
                .HasDefaultValue(true)
                .HasColumnName("isExpired");
            entity.Property(e => e.LastInsertTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Insert_Time");
            entity.Property(e => e.LastInsertUser).HasColumnName("Last_Insert_User");
            entity.Property(e => e.SubDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("Sub_Date");

            entity.HasOne(d => d.EduYearCodeNavigation).WithMany(p => p.StudentPlans)
                .HasForeignKey(d => d.EduYearCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Plan_Edu_Year");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.StudentPlanInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Plan_User");

            entity.HasOne(d => d.LastInsertUserNavigation).WithMany(p => p.StudentPlanLastInsertUserNavigations)
                .HasForeignKey(d => d.LastInsertUser)
                .HasConstraintName("FK_Student_Plan_User1");

            entity.HasOne(d => d.StudentCodeNavigation).WithMany(p => p.StudentPlans)
                .HasForeignKey(d => d.StudentCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Plan_Student");

            entity.HasOne(d => d.SubscriptionPlanCodeNavigation).WithMany(p => p.StudentPlans)
                .HasForeignKey(d => d.SubscriptionPlanCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Plan_Subscription_Plan");
        });

        modelBuilder.Entity<StudentSubjectPlan>(entity =>
        {
            entity.HasKey(e => new { e.StudentCode, e.SubjectCode, e.PlanCode, e.EduYearCode, e.SubDate });

            entity.ToTable("Student_Subject_Plan");

            entity.Property(e => e.StudentCode).HasColumnName("Student_Code");
            entity.Property(e => e.SubjectCode).HasColumnName("Subject_Code");
            entity.Property(e => e.PlanCode).HasColumnName("Plan_Code");
            entity.Property(e => e.EduYearCode).HasColumnName("Edu_Year_Code");
            entity.Property(e => e.SubDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("Sub_Date");
            entity.Property(e => e.ClassPrice)
                .HasColumnType("money")
                .HasColumnName("Class_Price");
            entity.Property(e => e.ClassesCount).HasColumnName("Classes_Count");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.LastInsertTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Insert_Time");
            entity.Property(e => e.LastInsertUser).HasColumnName("Last_Insert_User");

            entity.HasOne(d => d.EduYearCodeNavigation).WithMany(p => p.StudentSubjectPlans)
                .HasForeignKey(d => d.EduYearCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Subject_Plan_Edu_Year");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.StudentSubjectPlanInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Subject_Plan_User");

            entity.HasOne(d => d.LastInsertUserNavigation).WithMany(p => p.StudentSubjectPlanLastInsertUserNavigations)
                .HasForeignKey(d => d.LastInsertUser)
                .HasConstraintName("FK_Student_Subject_Plan_User1");

            entity.HasOne(d => d.PlanCodeNavigation).WithMany(p => p.StudentSubjectPlans)
                .HasForeignKey(d => d.PlanCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Subject_Plan_Subscription_Plan");

            entity.HasOne(d => d.StudentCodeNavigation).WithMany(p => p.StudentSubjectPlans)
                .HasForeignKey(d => d.StudentCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Subject_Plan_Student");

            entity.HasOne(d => d.SubjectCodeNavigation).WithMany(p => p.StudentSubjectPlans)
                .HasForeignKey(d => d.SubjectCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Student_Subject_Plan_Subject");
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            entity.HasKey(e => e.SubjectCode);

            entity.ToTable("Subject");

            entity.Property(e => e.SubjectCode).HasColumnName("Subject_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsPrimary).HasColumnName("isPrimary");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.SubjectName)
                .HasMaxLength(100)
                .HasColumnName("Subject_Name");
            entity.Property(e => e.SubjectTotal).HasColumnName("Subject_Total");
            entity.Property(e => e.YearCode).HasColumnName("Year_Code");
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(e => e.SubPlanCode).HasName("PK_Subscribtion_Plan");

            entity.ToTable("Subscription_Plan", tb =>
                {
                    tb.HasTrigger("Trigger_After_Insert_Update_Class_Price");
                    tb.HasTrigger("UpdateStudentPlanPrice");
                });

            entity.Property(e => e.SubPlanCode).HasColumnName("Sub_Plan_Code");
            entity.Property(e => e.Description)
                .HasMaxLength(150)
                .IsUnicode(false);
            entity.Property(e => e.ExpiryMonths)
                .HasDefaultValue(12.0)
                .HasColumnName("Expiry_Months");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.SubPlanName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("Sub_Plan_Name");
            entity.Property(e => e.TotalCount).HasColumnName("Total_Count");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.SubscriptionPlanInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Subscribtion_Plan_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.SubscriptionPlanLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Subscribtion_Plan_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.SubscriptionPlans)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Subscribtion_Plan_Root");
        });

        modelBuilder.Entity<Teach>(entity =>
        {
            entity.HasKey(e => new { e.TeacherCode, e.SubjectCode, e.EduYearCode, e.BranchCode, e.RootCode });

            entity.ToTable("Teach", tb => tb.HasTrigger("Trigger_After_Insert_Update_Center_Values"));

            entity.Property(e => e.TeacherCode).HasColumnName("Teacher_Code");
            entity.Property(e => e.SubjectCode).HasColumnName("Subject_Code");
            entity.Property(e => e.EduYearCode).HasColumnName("Edu_Year_Code");
            entity.Property(e => e.BranchCode).HasColumnName("Branch_Code");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.CenterAmount).HasColumnName("Center_Amount");
            entity.Property(e => e.CenterPercentage).HasColumnName("Center_Percentage");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.LastUpdateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");

            entity.HasOne(d => d.BranchCodeNavigation).WithMany(p => p.Teaches)
                .HasForeignKey(d => d.BranchCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teach_Branch");

            entity.HasOne(d => d.EduYearCodeNavigation).WithMany(p => p.Teaches)
                .HasForeignKey(d => d.EduYearCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teach_Edu_Year");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.TeachInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teach_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.TeachLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Teach_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Teaches)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teach_Root");

            entity.HasOne(d => d.SubjectCodeNavigation).WithMany(p => p.Teaches)
                .HasForeignKey(d => d.SubjectCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teach_Subject");

            entity.HasOne(d => d.TeacherCodeNavigation).WithMany(p => p.Teaches)
                .HasForeignKey(d => d.TeacherCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teach_Teacher");
        });

        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.HasKey(e => e.TeacherCode);

            entity.ToTable("Teacher");

            entity.Property(e => e.TeacherCode).HasColumnName("Teacher_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.IsStaff)
                .HasDefaultValue(true)
                .HasColumnName("isStaff");
            entity.Property(e => e.LastUpdateTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");
            entity.Property(e => e.TeacherAddress)
                .HasMaxLength(100)
                .HasColumnName("Teacher_Address");
            entity.Property(e => e.TeacherName)
                .HasMaxLength(100)
                .HasColumnName("Teacher_Name");
            entity.Property(e => e.TeacherPhone)
                .HasMaxLength(50)
                .HasColumnName("Teacher_Phone");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.TeacherInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teacher_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.TeacherLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Teacher_User1");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.Teachers)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Teacher_Root1");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserCode);

            entity.ToTable("User", tb => tb.HasTrigger("IncrementNoOfUsersInRoot"));

            entity.Property(e => e.UserCode)
                .ValueGeneratedNever()
                .HasColumnName("User_Code");
            entity.Property(e => e.GroupCode).HasColumnName("Group_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Password).HasMaxLength(32);
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.GroupCodeNavigation).WithMany(p => p.Users)
                .HasForeignKey(d => d.GroupCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_Group");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.InverseInsertUserNavigation)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_User_User1");
        });

        modelBuilder.Entity<WalletExam>(entity =>
        {
            entity.HasKey(e => e.WalletExamCode);

            entity.ToTable("Wallet_Exam");

            entity.Property(e => e.WalletExamCode).HasColumnName("Wallet_Exam_Code");
            entity.Property(e => e.DateStart).HasColumnName("Date_Start");
            entity.Property(e => e.ExpireDate).HasColumnName("Expire_date");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("isActive");
            entity.Property(e => e.OriginalCount).HasColumnName("Original_Count");
            entity.Property(e => e.RootCode).HasColumnName("Root_Code");

            entity.HasOne(d => d.RootCodeNavigation).WithMany(p => p.WalletExams)
                .HasForeignKey(d => d.RootCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Wallet_Exam_Root");
        });

        modelBuilder.Entity<Year>(entity =>
        {
            entity.HasKey(e => e.YearCode);

            entity.ToTable("Year");

            entity.Property(e => e.YearCode).HasColumnName("Year_Code");
            entity.Property(e => e.InsertTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("Insert_Time");
            entity.Property(e => e.InsertUser).HasColumnName("Insert_User");
            entity.Property(e => e.LastUpdateTime)
                .HasColumnType("datetime")
                .HasColumnName("Last_Update_Time");
            entity.Property(e => e.LastUpdateUser).HasColumnName("Last_Update_User");
            entity.Property(e => e.LevelCode).HasColumnName("Level_Code");
            entity.Property(e => e.YearName)
                .HasMaxLength(100)
                .HasColumnName("Year_Name");
            entity.Property(e => e.YearSort).HasColumnName("Year_Sort");

            entity.HasOne(d => d.InsertUserNavigation).WithMany(p => p.YearInsertUserNavigations)
                .HasForeignKey(d => d.InsertUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Year_User");

            entity.HasOne(d => d.LastUpdateUserNavigation).WithMany(p => p.YearLastUpdateUserNavigations)
                .HasForeignKey(d => d.LastUpdateUser)
                .HasConstraintName("FK_Year_User1");

            entity.HasOne(d => d.LevelCodeNavigation).WithMany(p => p.Years)
                .HasForeignKey(d => d.LevelCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Year_Level");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
