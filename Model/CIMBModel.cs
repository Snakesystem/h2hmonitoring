using System.Runtime.Serialization;
using System.Text;
using System.Xml.Serialization;

namespace H2HAPICore.Model.CIMB
{
    [DataContract]
    [XmlRoot(ElementName = "input")]
    [XmlType(Namespace = "java:prismagateway.service.doReceivePushData", TypeName = "Input")]
    public class Input
    {
        public Input() { }

        [DataMember]
        public string memberCode { get; set; }

        [DataMember]
        public string fileContents { get; set; }

        [DataMember]
        public long crc32 { get; set; }

        [DataMember]
        public string urlWS { get; set; }
    }

    [DataContract]
    [XmlRoot(ElementName = "output")]
    [XmlType(Namespace = "java:prismagateway.service.doReceivePushData", TypeName = "Output", IncludeInSchema = true)]
    public class Output
    {
        public Output() { }

        [DataMember(IsRequired = true)]
        [XmlElement(Namespace = "java:prismagateway.service.doReceivePushData")]
        public string @return { get; set; }

        [XmlAttribute(Namespace = "http://www.w3.org/2001/XMLSchema-instance")]
        public string type = "ns1:Output";

        [XmlNamespaceDeclarations()]
        public XmlSerializerNamespaces xmlns
        {
            get
            {
                XmlSerializerNamespaces xsn = new XmlSerializerNamespaces();
                xsn.Add("ns1", "java:prismagateway.service.doReceivePushData");
                return xsn;
            }
            set { }
        }
    }

    [DataContract]
    [XmlRoot("Message")]
    public class InvestorAcctStatementClass
    {
        [XmlAttribute("name")]
        public string name { get; set; }

        [XmlAttribute("type")]
        public string type { get; set; }

        [XmlElement("Field", Type = typeof(NiagaField))]
        public NiagaField[] fields { get; set; }

        [XmlElement("List", Type = typeof(NiagaList))]
        public NiagaList lists { get; set; }

        public InvestorAcctStatementClass()
        {
            fields = null;
            lists = null;
            name = "";
            type = "";
        }
    }

    [Serializable]
    public class NiagaField
    {
        [XmlAttribute("name")]
        public string name { get; set; }

        [XmlText]
        public string Text;

        public NiagaField()
        { }

        public NiagaField(string FieldName, string FieldValue)
        {
            name = FieldName;
            Text = FieldValue;
        }
    }

    [Serializable]
    public class NiagaList
    {
        [XmlAttribute("name")]
        public string name { get; set; }

        [XmlElement("Record", Type = typeof(NiagaRecord))]
        public NiagaRecord records { get; set; }

        public NiagaList()
        { }

        public NiagaList(string ListName)
        {
            name = ListName;
        }
    }

    [Serializable]
    public class NiagaRecord
    {
        [XmlAttribute("name")]
        public string name { get; set; }

        [XmlElement("List", Type = typeof(NiagaList))]
        public NiagaList lists { get; set; }

        [XmlElement("Field", Type = typeof(NiagaField))]
        public NiagaField[] fields { get; set; }

        public NiagaRecord()
        { }

        public NiagaRecord(string RecName)
        {
            name = RecName;
        }
    }



    public class Token
    {
        public string access_token { get; set; }
    }
    public class Settings
    {
        public string URL { get; set; }
        public string CorpID { get; set; }
        public string SecurityWord { get; set; }
    }

    public class CIMB_OnlineTransfer
    {
        public long AutoNID { get; set; }

        public int BankInstructionNID { get; set; }

        public string TransferId { get; set; }

        public string TxnDate { get; set; }

        public string DebitAcctNo { get; set; }

        public string BenName { get; set; }

        public string BenBankName { get; set; }

        public string BenBankAddr1 { get; set; }

        public string BenBankBranch { get; set; }

        public string BenBankCode { get; set; }

        public string BenBankSWIFT { get; set; }

        public string BenBankCity { get; set; }

        public string BenBankCountry { get; set; }

        public string CurrCd { get; set; }

        public string Memo { get; set; }

        public string StatusCode { get; set; }

        public string StatusMessage { get; set; }

        public string InstructDate { get; set; }

        public string RequestID { get; set; }

        public string ServiceCode { get; set; }

        public string CorpID { get; set; }

        public string TxnRequestDateTime { get; set; }

        public string Amount { get; set; }

        public string ClientID { get; set; }

        public string BenAcctNo { get; set; }

        public int BatchID { get; set; }

        public DateTime EntryTime { get; set; }

        public string BankInstructionType { get; set; }

        public DateTime SendTime { get; set; }



    }

    public class InHouseTransferRequest
    {
        public string transferId { get; set; }
        public string txnDate { get; set; }
        public string debitAcctNo { get; set; }
        public string benAcctNo { get; set; }
        public string benName { get; set; }
        public string benBankName { get; set; }
        public string benBankAddr1 { get; set; }
        public string benBankBranch { get; set; }
        public string benBankCode { get; set; }
        public string benBankSWIFT { get; set; }
        public string currCd { get; set; }
        public string amount { get; set; }
        public string memo { get; set; }
        public string requestID { get; set; }
        public string txnRequestDateTime { get; set; }
        public string serviceCode { get; set; }
        public string token { get; set; }

    }

    public class TransferResponse
    {
        public string statusode { get; set; }
        public string statusmessage { get; set; }
    }

}
