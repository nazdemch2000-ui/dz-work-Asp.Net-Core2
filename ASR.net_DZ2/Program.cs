using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Text;
using System.Xml.Linq;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//Получаем сервис IConfiguration, через свойство Services 
var configurationService = app.Services.GetService<IConfiguration>();

//С помощью индексатора обращаемся к нужной строке подключения.
//Необходимо указать полный путь к секции, через двоеточие
string connectionString = configurationService["ConnectionStrings:DefaultConnection"];

app.Run(async (context) =>
{
    var response = context.Response;
    var request = context.Request;
    response.ContentType = "text/html; charset=utf-8";

    //При переходе на главную страницу, считываем всех пользователей
    if (request.Path == "/")
    {
        string search = request.Query["search"].ToString();
        string sort = request.Query["sort"].ToString();
        List<User> users = new List<User>();
        string orderBy = sort switch
        {
            "nameDesc" => "name DESC",
            "age" => "age ASC",
            "ageDesc" => "age DESC",
            _ => "name ASC"
        };
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            SqlCommand checkCommand = new SqlCommand(
    "SELECT DB_NAME()",
    connection
);

            string databaseName = (string)await checkCommand.ExecuteScalarAsync();

            Console.WriteLine($"Database: {databaseName}");
            SqlCommand command = new SqlCommand(
                $"SELECT Id, Name, Age FROM Users WHERE Name LIKE @search  ORDER BY {orderBy}",
                connection);

            command.Parameters.AddWithValue("@search", "%" + search + "%");
           

            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    users.Add(new User(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetInt32(2)
                    ));
                }
            }
        }

        await response.WriteAsync(
            GenerateHtmlPage(ToTable(users), "All Users from DataBase", search)
        );
       
    }
    else if (request.Path == "/add" && request.Method == "GET")
    {
        string form = """
        <form action="/add" method="POST">
            <div class="mb-3">
                <label for="name" class="form-label">Name</label>
                <input type="text" id="name" name="name"
                       class="form-control" required>
            </div>

            <div class="mb-3">
                <label for="age" class="form-label">Age</label>
                <input type="number" id="age" name="age"
                       class="form-control" min="1" required>
            </div>

            <button type="submit" class="btn btn-primary">
                Add User
            </button>

            <a href="/" class="btn btn-secondary">Back</a>
        </form>
        """;

        await response.WriteAsync(GenerateHtmlPage(form, "Add User"));
    }
    else if (request.Path == "/add" && request.Method == "POST")
    {

        var form = await request.ReadFormAsync();
        string name = form["name"].ToString();
        int age = int.Parse(form["age"].ToString());
        int newId = AddUser(name, age, connectionString);
        response.Redirect("/");
        return;
    }
    else if(request.Path == "/deleteUser" && request.Method == "GET")
    {
        if (!int.TryParse(request.Query["id"], out int id))
        {
            await response.WriteAsync("Not found Id");
            return;
        }
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = new SqlCommand(
            "DELETE FROM Users WHERE Id = @id", connection);

            command.Parameters.AddWithValue("@id", id);

            command.ExecuteNonQuery();
        }
        response.Redirect("/");
        return;
    }

    else if (request.Path == "/changeUser" && request.Method == "GET")
    {
        if (!int.TryParse(request.Query["id"], out int id))
        {
            await response.WriteAsync("Not found Id");
            return;
        }

        User? user = null;

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();

            SqlCommand command = new SqlCommand(
                "SELECT Id, Name, Age FROM Users WHERE Id = @id",
                connection);

            command.Parameters.AddWithValue("@id", id);

            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    user = new User(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetInt32(2));
                }
            }
        }

        if (user == null)
        {
            response.StatusCode = 404;
            await response.WriteAsync("User not found");
            return;
        }

        string form = $"""
        <form action="/changeUser" method="POST">
            <input type="hidden" name="id" value="{user.Id}">

            <div class="mb-3">
                <label for="name" class="form-label">Name</label>
                <input value="{user.Name}" type="text" id="name" name="name"
                       class="form-control" required>
            </div>

            <div class="mb-3">
                <label for="age" class="form-label">Age</label>
                <input value="{user.Age}" type="number" id="age" name="age"
                       class="form-control" min="1" required>
            </div>

            <button type="submit" class="btn btn-primary">
                Change
            </button>

            <a href="/" class="btn btn-secondary">Back</a>
        </form>
        """;

        await response.WriteAsync(GenerateHtmlPage(form, "Change User"));
    }
    else if(request.Path == "/changeUser" && request.Method == "POST")
    {

        var form = await request.ReadFormAsync();
        string name = form["name"].ToString();

        if (!int.TryParse(form["age"], out int age) ||
            !int.TryParse(form["id"], out int id))
        {
            response.StatusCode = 400;
            await response.WriteAsync("Invalid Id or Age");
            return;
        }
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = new SqlCommand(
            "UPDATE Users SET Name = @Name, Age = @Age WHERE Id = @Id", connection);

            command.Parameters.AddWithValue("@Name", name);
            command.Parameters.AddWithValue("@Age", age);
            command.Parameters.AddWithValue("@id", id);

            command.ExecuteNonQuery();
        }
        

        response.Redirect("/");
        return;
    }
    else
    {
        response.StatusCode = 404;
        await response.WriteAsJsonAsync("Page Not Found");
    }
});


app.Run();

static string GenerateHtmlPage(string body, string header ,string search = "")
{
    string html = $"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0-alpha3/dist/css/bootstrap.min.css" rel="stylesheet" 
            integrity="sha384-KK94CHFLLe+nY2dmCWGMq91rCGa5gtU4mk92HdvYe+M/SXH301p5ILy+dN9+nJOZ" crossorigin="anonymous">
            <title>{header}</title>
        </head>
        <body>
        <div class="container">
        <h2 class="d-flex justify-content-center">{header}</h2>
        <div class="mt-5"></div>
                <form action="/" method="GET" class="d-flex gap-2 mt-3 mb-3">
            <input type="text" name="search" class="form-control"
                   placeholder="Search by name"
                   value="{System.Net.WebUtility.HtmlEncode(search)}">
                        <select name="sort" class="form-select">
            <option value="name">Name A-Z</option>
            <option value="nameDesc">Name Z-A</option>
            <option value="age">Age ↑</option>
            <option value="ageDesc">Age ↓</option>
        </select>
            <button type="submit" class="btn btn-primary">Search</button>
            <a href="/" class="btn btn-secondary">Reset</a>
        </form>
        <a href='/add' class='btn btn-success'>Add User</a>
        {body}
            <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0-alpha3/dist/js/bootstrap.bundle.min.js" 
            integrity="sha384-ENjdO4Dr2bkBIFxQpeoTz1HIcje39Wm4jDKdf19U8gI4ddQ3GYNS7NTKfAdVQSZe" crossorigin="anonymous"></script>
        </div>
        </body>
        </html>
        """;
    return html;
}

static string ToTable(List<User> users)
{
    StringBuilder st = new StringBuilder("<table class=\"table\"><tr><th>Id</th><th>Name</th><th>Age</th></tr>");
    foreach (User user in users)
    {
        st.Append($"<tr><td>{user.Id}</td><td>{user.Name}</td><td>{user.Age}</td><td class=\"text-end\"><a href=\"/changeUser?Id={user.Id}\" class=\"btn btn-primary\">Change</a><a href=\"/deleteUser?Id={user.Id}\" class=\"btn btn-danger\">DELETE</a></td></tr>");
    }
    st.Append("</table>");
    return st.ToString();
}

static int AddUser(string name, int age, string connectionString)
{
    using (SqlConnection connection = new SqlConnection(connectionString))
    {
        connection.Open();
        SqlCommand command = new SqlCommand("INSERT INTO Users (Name, Age) VALUES (@Name, @Age); SELECT SCOPE_IDENTITY();", connection);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@Age", age);
        int newId = Convert.ToInt32(command.ExecuteScalar());
        return newId;
    }
}
record User(int Id, string Name, int Age)
{
    public User(string Name, int Age) : this(0, Name, Age)
    {

    }
}

