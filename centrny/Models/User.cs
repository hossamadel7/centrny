using System;
using System.Collections.Generic;

namespace centrny.Models;

public partial class User
{
    public int UserCode { get; set; }

    public string Name { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public int GroupCode { get; set; }

    public bool IsActive { get; set; }

    public int InsertUser { get; set; }

    public DateTime InsertTime { get; set; }

    public int? LastUpdateUser { get; set; }

    public DateTime? LastUpdateTime { get; set; }

    public virtual ICollection<Answer> AnswerInsertUserNavigations { get; set; } = new List<Answer>();

    public virtual ICollection<Answer> AnswerLastUpdateUserNavigations { get; set; } = new List<Answer>();

    public virtual ICollection<Branch> BranchInsertUserNavigations { get; set; } = new List<Branch>();

    public virtual ICollection<Branch> BranchLastUpdateUserNavigations { get; set; } = new List<Branch>();

    public virtual ICollection<Center> CenterInsertUserNavigations { get; set; } = new List<Center>();

    public virtual ICollection<Center> CenterLastInsertUserNavigations { get; set; } = new List<Center>();

    public virtual ICollection<Class> ClassInsertUserNavigations { get; set; } = new List<Class>();

    public virtual ICollection<Class> ClassLastUpdateUserNavigations { get; set; } = new List<Class>();

    public virtual ICollection<Employee> EmployeeInsertUserNavigations { get; set; } = new List<Employee>();

    public virtual ICollection<Employee> EmployeeLastUpdateUserNavigations { get; set; } = new List<Employee>();

    public virtual ICollection<Employee> EmployeeUserCodeNavigations { get; set; } = new List<Employee>();

    public virtual ICollection<Exam> ExamInsertUserNavigations { get; set; } = new List<Exam>();

    public virtual ICollection<Exam> ExamLastUpdateUserNavigations { get; set; } = new List<Exam>();

    public virtual ICollection<ExamQuestion> ExamQuestionInsertUserNavigations { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<ExamQuestion> ExamQuestionLastUpdateUserNavigations { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<Expense> ExpenseInsertUserNavigations { get; set; } = new List<Expense>();

    public virtual ICollection<Expense> ExpenseLastUpdateUserNavigations { get; set; } = new List<Expense>();

    public virtual Group GroupCodeNavigation { get; set; } = null!;

    public virtual ICollection<Group> GroupInsertUserNavigations { get; set; } = new List<Group>();

    public virtual ICollection<Group> GroupLastUpdateUserNavigations { get; set; } = new List<Group>();

    public virtual ICollection<Hall> HallInsertUserNavigations { get; set; } = new List<Hall>();

    public virtual ICollection<Hall> HallLastUpdateUserNavigations { get; set; } = new List<Hall>();

    public virtual User InsertUserNavigation { get; set; } = null!;

    public virtual ICollection<User> InverseInsertUserNavigation { get; set; } = new List<User>();

    public virtual ICollection<Item> ItemInsertUserNavigations { get; set; } = new List<Item>();

    public virtual ICollection<Item> ItemLastUpdateUserNavigations { get; set; } = new List<Item>();

    public virtual ICollection<Lesson> LessonInsertUserNavigations { get; set; } = new List<Lesson>();

    public virtual ICollection<Lesson> LessonLastUpdateUserNavigations { get; set; } = new List<Lesson>();

    public virtual ICollection<Level> LevelInsertUserNavigations { get; set; } = new List<Level>();

    public virtual ICollection<Level> LevelLastUpdateUserNavigations { get; set; } = new List<Level>();

    public virtual ICollection<Page> PageInsertUserNavigations { get; set; } = new List<Page>();

    public virtual ICollection<Page> PageLastUpdateUserNavigations { get; set; } = new List<Page>();

    public virtual ICollection<PlanSubject> PlanSubjectInsertUserNavigations { get; set; } = new List<PlanSubject>();

    public virtual ICollection<PlanSubject> PlanSubjectLastInsertUserNavigations { get; set; } = new List<PlanSubject>();

    public virtual ICollection<PositionType> PositionTypeInsertUserNavigations { get; set; } = new List<PositionType>();

    public virtual ICollection<PositionType> PositionTypeLastUpdateUserNavigations { get; set; } = new List<PositionType>();

    public virtual ICollection<Question> QuestionInsertUserNavigations { get; set; } = new List<Question>();

    public virtual ICollection<Question> QuestionLastUpdateUserNavigations { get; set; } = new List<Question>();

    public virtual ICollection<Student> StudentInsertUserNavigations { get; set; } = new List<Student>();

    public virtual ICollection<Student> StudentLastInsertUserNavigations { get; set; } = new List<Student>();

    public virtual ICollection<StudentPlan> StudentPlanInsertUserNavigations { get; set; } = new List<StudentPlan>();

    public virtual ICollection<StudentPlan> StudentPlanLastInsertUserNavigations { get; set; } = new List<StudentPlan>();

    public virtual ICollection<StudentSubjectPlan> StudentSubjectPlanInsertUserNavigations { get; set; } = new List<StudentSubjectPlan>();

    public virtual ICollection<StudentSubjectPlan> StudentSubjectPlanLastInsertUserNavigations { get; set; } = new List<StudentSubjectPlan>();

    public virtual ICollection<SubscriptionPlan> SubscriptionPlanInsertUserNavigations { get; set; } = new List<SubscriptionPlan>();

    public virtual ICollection<SubscriptionPlan> SubscriptionPlanLastUpdateUserNavigations { get; set; } = new List<SubscriptionPlan>();

    public virtual ICollection<Teach> TeachInsertUserNavigations { get; set; } = new List<Teach>();

    public virtual ICollection<Teach> TeachLastUpdateUserNavigations { get; set; } = new List<Teach>();

    public virtual ICollection<Teacher> TeacherInsertUserNavigations { get; set; } = new List<Teacher>();

    public virtual ICollection<Teacher> TeacherLastUpdateUserNavigations { get; set; } = new List<Teacher>();

    public virtual ICollection<Year> YearInsertUserNavigations { get; set; } = new List<Year>();

    public virtual ICollection<Year> YearLastUpdateUserNavigations { get; set; } = new List<Year>();
}
