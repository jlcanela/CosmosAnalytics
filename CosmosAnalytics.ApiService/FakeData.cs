using AutoBogus;
using ProjectModels;

namespace FakeData
{
    public class TaskFaker : AutoFaker<ProjectModels.Task>
    {
        public TaskFaker()
        {
            RuleFor(t => t.Assignee, f => f.Name.FullName());
            // You can add more custom rules for Task here if needed
        }
    }
    public class CustomProjectFaker : AutoFaker<Project>
    {
        public CustomProjectFaker()
        {
            RuleFor(p => p.Id, f => f.Random.Guid().ToString());
            RuleFor(p => p.Name, f => f.Lorem.Word());
            // Use Bogus Faker methods for specific properties
            RuleFor(p => p.Name, f => f.Company.CompanyName());
            RuleFor(p => p.Status, f => f.PickRandom<ProjectStatus>());
           // Fixed: Single-parameter lambda + convert to array
            RuleFor(p => p.Tasks, f => new TaskFaker().Generate(f.Random.Int(3, 7)).ToArray());
            // Add more custom rules as needed
        }
    }
}