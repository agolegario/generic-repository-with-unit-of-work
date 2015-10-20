using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.Entity.ModelConfiguration;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleInjector;
using SimpleInjector.Extensions.LifetimeScoping;

namespace UnitTestRepositoryGenericUnitOfWork
{
    #region  Tests
    [TestClass]
    public class UnitTestGenericRepository
    {
        private Container _container;

        [TestInitialize]
        public void Inicialize()
        {
            //Inicialize AutoMapperConfig
            AutoMapperConfig.RegisterMappings();
            //Inicialize new Container Injector
            _container = new Container();
            // Set LiveTimeScope using in MVC and WEB API
            _container.Options.DefaultScopedLifestyle = new LifetimeScopeLifestyle();

            //UnitOfWork
            var registration = Lifestyle.Singleton.CreateRegistration<MyContext>(_container);
            _container.AddRegistration(typeof(IMyContext), registration);
            _container.AddRegistration(typeof(IUnitOfWork), registration);

            //Repositories
            _container.Register(typeof(IRepositoryBase<>),typeof(RepositoryBase<>));
            _container.Register<IPersonRepository, PersonRepository>();
            _container.Register<IPersonService, PersonService>();
        }
        [TestMethod]
        public void Test_Injector_Singleton()
        {
            var contexto1 = _container.GetInstance<IUnitOfWork>();
            var contexto2 = _container.GetInstance<IMyContext>();
            Assert.AreSame(contexto1, contexto2);
        }
        [TestMethod]
        public void Test_Injector_Scope()
        {
            using (_container.BeginLifetimeScope())
            {
                var contexto1 = _container.GetInstance<IUnitOfWork>();
                var contexto2 = _container.GetInstance<IMyContext>();
                Assert.AreSame(contexto1, contexto2);
            }
        }
        [TestMethod]
        public void Test_Crud_App_To_Domain()
        {
            var service = _container.GetInstance<IPersonService>();
            var pessoaModel = new PersonModel { Nome = "TESTE-CRUD" };
            service.Add(pessoaModel);

            pessoaModel = service.GetByName("TESTE-CRUD").FirstOrDefault();

            if (pessoaModel == null) return;

            pessoaModel.Nome = "TESTE";
            service.Update(pessoaModel);

            pessoaModel = service.Find(s => s.Nome.Equals("TESTE")).FirstOrDefault();

            if (pessoaModel == null) return;

            pessoaModel.Nome = "TESTE-CRUD";
            service.Update(pessoaModel);

            pessoaModel = service.Find(s => s.Nome.Equals("TESTE-CRUD")).FirstOrDefault();
            if (pessoaModel == null) return;
            service.Delete(pessoaModel);

            pessoaModel = service.Find(s => s.Nome.Equals("TESTE-CRUD")).FirstOrDefault();
            Assert.IsNull(pessoaModel);

            service.Dispose();
        }
    }
    #endregion

    #region  Application
    [NotMapped]
    public class PersonModel : Person
    {
        public new int Id { get; set; }
    }
    public interface IPersonService
    {
        void Add(PersonModel person);
        void Update(PersonModel person);
        void Delete(PersonModel person);
        PersonModel GetById(int id);
        IEnumerable<PersonModel> GetAll();
        IEnumerable<PersonModel> Find(Expression<Func<Person, bool>> predicate);
        IEnumerable<PersonModel> GetByName(string nome);
        void Dispose();
    }
    public class PersonService : IPersonService
    {
        private readonly IPersonRepository _personRepository;
        private readonly IUnitOfWork _unitOfWork;

        public PersonService(IUnitOfWork unitOfWork, IPersonRepository personRepository)
        {
            _unitOfWork = unitOfWork;
            _personRepository = personRepository;
        }
        public void Add(PersonModel person)
        {
            var produto = Mapper.Map<PersonModel, Person>(person);
            _personRepository.Add(produto);
            _unitOfWork.Commit();
        }
        public void Update(PersonModel person)
        {
            var produto = Mapper.Map<PersonModel, Person>(person);
            _personRepository.Update(produto);
            _unitOfWork.Commit();
        }
        public void Delete(PersonModel person)
        {
            var produto = Mapper.Map<PersonModel, Person>(person);
            _personRepository.Delete(produto.Id);
            _unitOfWork.Commit();
        }
        public PersonModel GetById(int id)
        {
            return Mapper.Map<Person, PersonModel>(_personRepository.GetById(id));
        }
        public IEnumerable<PersonModel> GetAll()
        {
            return Mapper.Map<IEnumerable<Person>, IEnumerable<PersonModel>>(_personRepository.GetAll());
        }
        public IEnumerable<PersonModel> Find(Expression<Func<Person, bool>> predicate)
        {
            return Mapper.Map<IEnumerable<Person>, IEnumerable<PersonModel>>(_personRepository.Find(predicate));
        }
        public IEnumerable<PersonModel> GetByName(string nome)
        {
            return Mapper.Map<IEnumerable<Person>, IEnumerable<PersonModel>>(_personRepository.GetByName(nome));
        }
        public void Dispose()
        {
            _personRepository.Dispose();
            GC.SuppressFinalize(this);
        }
    }
    public class AutoMapperConfig
    {
        public static void RegisterMappings()
        {
            Mapper.Initialize(x =>
            {
                x.AddProfile<DomainToApplicationProfile>();
                x.AddProfile<ApplicationToDomainProfile>();
            });
        }
    }
    public class ApplicationToDomainProfile : Profile
    {
        public override string ProfileName => "ApplicationToDomainProfile";

        protected override void Configure()
        {
            Mapper.CreateMap<PersonModel, Person>();
        }
    }
    public class DomainToApplicationProfile : Profile
    {
        public override string ProfileName => "DomainToApplicationProfile";

        protected override void Configure()
        {
            Mapper.CreateMap<Person, PersonModel>();
        }
    }
    #endregion

    #region  Domain
    public class Person
    {
        public int Id { get; set; }
        public string Nome { get; set; }
    }
    public interface IUnitOfWork
    {
        int Commit();
    }
    public interface IRepositoryBase<T> : IDisposable where T : class
    {
        void Add(T obj);
        void Update(T obj);
        void Delete(int id);
        T GetById(int id);
        IEnumerable<T> GetAll();
        IEnumerable<T> Find(Expression<Func<T, bool>> predicate);
    }
    public interface IPersonRepository : IRepositoryBase<Person>
    {
        IEnumerable<Person> GetByName(string nome);
    }
    #endregion

    #region  Infra
    public class RepositoryBase<T> : IRepositoryBase<T> where T : class
    {
        private readonly IMyContext _context;
        public RepositoryBase(IMyContext context)
        {
            _context = context;
        }
        public void Add(T obj)
        {
            _context.Set<T>().Add(obj);
        }
        public void Update(T obj)
        {
            _context.Set<T>().AddOrUpdate(obj);
        }
        public void Delete(int id)
        {
            var generic = _context.Set<T>().Find(id);
             if (generic == null) return;
             _context.Set<T>().Remove(generic);
        }
        public T GetById(int id)
        {
            return _context.Set<T>().Find(id);
        }
        public IEnumerable<T> GetAll()
        {
            return _context.Set<T>().ToList();
        }
        public IEnumerable<T> Find(Expression<Func<T, bool>> predicate)
        {
            return _context.Set<T>().Where(predicate).ToList();
        }
        public void Dispose()
        {
            _context.Disponse();
            GC.SuppressFinalize(this);
        }
    }
    public class PersonRepository : RepositoryBase<Person>, IPersonRepository
    {
        private readonly IMyContext _context;
        public PersonRepository(IMyContext context) : base(context)
        {
            _context = context;
        }
        public IEnumerable<Person> GetByName(string nome)
        {
            return _context.Set<Person>().Where(s => s.Nome.Contains(nome));
        } 
    }
    public interface IMyContext : IUnitOfWork
    {
        IDbSet<T> Set<T>() where T : class;
        DbEntityEntry Entry(object entity);
        void Disponse();
    }
    public class MyContext : DbContext, IMyContext
    {
        public MyContext() : base("Conn")
        {
            Database.SetInitializer<MyContext>(null);
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }
        public int Commit()
        {
            return SaveChanges();
        }
        public new IDbSet<T> Set<T>() where T : class
        {
            return base.Set<T>();
        }
        public void Disponse()
        {
            Dispose();
            GC.SuppressFinalize(this);
        }
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            modelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();
            modelBuilder.Conventions.Remove<ManyToManyCascadeDeleteConvention>();

            modelBuilder.Configurations.Add(new PersonMap());
            base.OnModelCreating(modelBuilder);
        }
    }
    public class PersonMap : EntityTypeConfiguration<Person>
    {
        public PersonMap()
        {
            ToTable("PERSON");
            HasKey(s => s.Id);
            Property(s => s.Nome).HasMaxLength(100).HasColumnType("varchar");
        }
    }
    #endregion

}
