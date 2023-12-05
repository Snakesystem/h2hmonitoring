using H2HAPICore.Context;
using H2HAPICore.Services;
using NLog;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Xml;
using SoapCore;
using System.ServiceModel.Channels;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace H2HAPICore
{
    public class Program
    {
        public static IConfiguration configuration { get; private set; }
        public static Logger logger;

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var services = builder.Services;
            var env = builder.Environment;

            configuration = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
             .Build();

            logger = LogManager.LoadConfiguration(string.Concat(Directory.GetCurrentDirectory(), "/nlog.config")).GetCurrentClassLogger();


            services.AddCors();

            services.AddSingleton<ILoggerManager, LoggerManager>();
            services.AddSingleton<DapperContext>();
            Dapper.SqlMapper.Settings.CommandTimeout = 0;

            services.AddControllers()
                .AddMvcOptions(x =>
                {
                    x.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                })
                .AddJsonOptions(x =>
                {
                    x.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    x.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    x.JsonSerializerOptions.PropertyNamingPolicy = null;
                });

            services.AddScoped<IBCAService, BCAService>();
            services.AddScoped<IGenericService, GenericService>();
            services.AddScoped<IPermataService, PermataService>();
            services.AddScoped<IBRIService, BRIService>();
            services.AddScoped<ICIMBInstructionService, CIMBInstructionService>();


            services.AddSoapCore();
            services.TryAddSingleton<ICIMBDepositService, CIMBDepositService>();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
            {
                app.UseCors(x => x
                   .AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader());
                //app.UseMiddleware<ErrorHandlerMiddleware>();
                app.MapControllers();

            }

            SoapEncoderOptions encoderOptions = new SoapEncoderOptions();
            encoderOptions.PortName = "doReceivePushData";
            encoderOptions.BindingName = "doReceivePushDataSoapBinding";

            IApplicationBuilder abc = (IApplicationBuilder)app;

            abc.UseSoapEndpoint<ICIMBDepositService, CustomNamespaceMessage>("/cimbnotification", encoderOptions, SoapSerializer.XmlSerializer, omitXmlDeclaration: false);

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                c.DocumentTitle = "H2HCore";
            });

            app.Run();
        }
    }


    public class CustomNamespaceMessage : CustomMessage
    {
        //private readonly IEnumerable<string> _customNamespaces = new string[]();
        private const string EnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

        public CustomNamespaceMessage() { }

        public CustomNamespaceMessage(Message message) : base(message) { }

        //protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        //{
        //    //foreach (string ns in _customNamespaces)
        //    //{
        //    //    var tokens = ns.Split(new char[] { ':' }, 2);
        //    //    writer.WriteAttributeString("xmlns", tokens[0], null, tokens[1]);
        //    //}

        //    this.Message.WriteBodyContents(writer);
        //}



        public override MessageHeaders Headers => Message.Headers;

        public override MessageProperties Properties => Message.Properties;

        public override MessageVersion Version => Message.Version;

        protected override void OnWriteStartHeaders(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement(Version.Envelope.NamespacePrefix(NamespaceManager), "Header", Version.Envelope.Namespace());
        }


        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("Body", EnvelopeNamespace);
        }

        //protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        //{
        //    writer.WriteStartElement("soapenv", "Envelope", Version.Envelope.Namespace());
        //}

        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            writer.WriteStartDocument();
            //var prefix = Version.Envelope.NamespacePrefix(NamespaceManager);
            writer.WriteStartElement("soapenv", "Envelope", EnvelopeNamespace);
            writer.WriteXmlnsAttribute("soapenv", EnvelopeNamespace);
            var xsdPrefix = Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(NamespaceManager, "xsd", Namespaces.XMLNS_XSD);
            writer.WriteXmlnsAttribute(xsdPrefix, Namespaces.XMLNS_XSD);
            var xsiPrefix = Namespaces.AddNamespaceIfNotAlreadyPresentAndGetPrefix(NamespaceManager, "xsi", Namespaces.XMLNS_XSI);
            writer.WriteXmlnsAttribute(xsiPrefix, Namespaces.XMLNS_XSI);

            if (AdditionalEnvelopeXmlnsAttributes != null)
            {
                foreach (var rec in AdditionalEnvelopeXmlnsAttributes)
                {
                    writer.WriteXmlnsAttribute(rec.Key, rec.Value);
                }
            }
        }
    }
}







