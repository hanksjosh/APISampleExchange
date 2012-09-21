using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using APISampleExchange.JustWareAPI;

namespace APISampleExchange
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            InsertDataFromXml();
        }

        private void InsertDataFromXml()
        {
            JustWareApiClient apiClient = new JustWareApiClient();
            apiClient.ClientCredentials.UserName.UserName = "USERNAME";
            apiClient.ClientCredentials.UserName.Password = "PASSWORD";

            XDocument doc = XDocument.Load("CaseImport.xml");
            List<XElement> cases = doc.Descendants("Case").ToList();
            foreach (var singleCase in cases)
            {
                XElement defXml = cases.Descendants("Defendant").First();
                Name defendant = InsertDefendent(apiClient, defXml);
                Name policeOfficer = CheckInsertPO(apiClient, singleCase);

                Case c = InsertCase(apiClient, singleCase, defendant, policeOfficer);

            }

        }


        private Name InsertDefendent(JustWareApiClient apiClient, XElement defXml)
        {
            XElement defNameXml = defXml.Descendants("Name").First();
            var nameValues = GetChildValues(defNameXml);
            Name defendant = CreateName(nameValues);
            defendant = AddAddressIfExists(defXml, defendant);
            defendant = AddPhoneIfExists(defXml, defendant);

            List<Key> returnedKeys = apiClient.Submit(defendant);
            defendant.ID = returnedKeys.First(k => k.TypeName == "Name").NewID;

            return defendant;
        }

        private Name CheckInsertPO(JustWareApiClient apiClient, XElement caseXml)
        {
            XElement poXml = caseXml.Descendants("PoliceOfficer").First();
            if (poXml != null)
            {
                Name poName = new Name();
                XElement poNameXml = poXml.Descendants("Name").First();
                var nameValues = GetChildValues(poNameXml);
                string query = String.Format("Last = \"{0}\" AND First = \"{1}\"", nameValues["Last"], nameValues["First"]);
                List<Name> poNames = apiClient.FindNames(query, null);
                if (poNames.Count > 0)
                {
                    poName = poNames.First();
                }
                else
                {
                    poName = CreateName(nameValues);
                    List<Key> returnedKeys = apiClient.Submit(poName);
                    poName.ID = returnedKeys.First(k => k.TypeName == "Name").NewID;
                }
                return poName;
            }
            return null;
        }

        private Name CreateName(Dictionary<string, string> nameValues)
        {
            Name name = new Name()
                            {
                                Last = nameValues["Last"],
                                First = nameValues["First"],
                                Middle = nameValues["Middle"],
                                Height = nameValues["Height"],
                                Weight = short.Parse(nameValues["Weight"]),
                                ID = Int32.Parse(nameValues["ID"]),
                                Operation = OperationType.Insert
                            };
            if (!String.IsNullOrEmpty(nameValues["DOB"]))
            {
                name.DateOfBirth = DateTime.Parse(nameValues["DOB"]);
            }

            if (!String.IsNullOrWhiteSpace(nameValues["SSN"]))
            {
                NameNumber nameNumber = new NameNumber();
                nameNumber.Number = nameValues["SSN"];
                nameNumber.TypeCode = "SSN";
                nameNumber.Operation = OperationType.Insert;


                name.Numbers = new List<NameNumber>();
                name.Numbers.Add(nameNumber);
            }
            return name;
        }

        private Name AddAddressIfExists(XElement defXml, Name defendant)
        {
            XElement addressXml = defXml.Descendants("Address").FirstOrDefault();
            if (addressXml != null)
            {
                var addressValues = GetChildValues(addressXml);
                Address address = new Address()
                                      {
                                          ID = Int32.Parse(addressValues["ID"]),
                                          StreetAddress = addressValues["Address"],
                                          TypeCode = "HA",
                                          City = addressValues["City"],
                                          StateCode = addressValues["State"],
                                          Zip = addressValues["Zip"],
                                          Operation = OperationType.Insert
                                      };

                defendant.Addresses = new List<Address>() {address};
            }
            return defendant;
        }

        private Name AddPhoneIfExists(XElement defXml, Name defendant)
        {
            XElement phoneXml = defXml.Descendants("Phone").FirstOrDefault();
            if (phoneXml != null)
            {
                var phoneValues = GetChildValues(phoneXml);
                Phone phone = new Phone()
                {
                    ID = Int32.Parse(phoneValues["ID"]),
                    Number = phoneValues["Number"],
                    TypeCode = "HP",
                   Operation = OperationType.Insert
                };

                defendant.Phones = new List<Phone>() { phone };
            }
            return defendant;
        }


        private Case InsertCase(JustWareApiClient apiClient, XElement caseXml, Name defendant, Name policeOfficer)
        {
            Case c = new Case();
            c.Operation = OperationType.Insert;
            c.TypeCode = "F";
            c.StatusCode = "ACTIV";
            c.AgencyAddedByCode = "NDT";
            c.StatusDate = DateTime.Now;
            c.ReceivedDate = DateTime.Now;


            CaseInvolvedName def = CreateInvolvedName("DEF", defendant.ID);
            CaseInvolvedName po = CreateInvolvedName("PO", policeOfficer.ID);
            c.CaseInvolvedNames = new List<CaseInvolvedName>() {def, po};

            var chargeElements = caseXml.Descendants("Charge");
            foreach (var chargeElement in chargeElements)
            {
                Charge charge = CreateNewCharge(apiClient, chargeElement);
                if (charge == null) continue;
                if (c.Charges == null)
                {
                    c.Charges = new List<Charge>();
                }
                c.Charges.Add(charge);
            }

            List<Key> returnedKeys = apiClient.Submit(c);
            string newCaseID = returnedKeys.First(k => k.TypeName == "Case").NewCaseID;

            Case finishedCase = apiClient.GetCase(newCaseID, null);
            return finishedCase;
        }

        private CaseInvolvedName CreateInvolvedName(string involvementCode, int id)
        {
            CaseInvolvedName ciName = new CaseInvolvedName();
            ciName.Operation = OperationType.Insert;
            ciName.InvolvementCode = involvementCode;
            ciName.NameID = id;
            return ciName;
        }

        private Charge CreateNewCharge(JustWareApiClient api, XElement chargeXml)
        {
            var chargeValues = GetChildValues(chargeXml);
            Charge charge = new Charge();
            charge.Operation = OperationType.Insert;
            string query = String.Format("ChargeID = \"{0}\"", chargeValues["Code"]);
            Statute statute = api.FindStatutes(query, null).FirstOrDefault();
            if (statute != null)
            {
                charge.StatuteID = statute.ID;
            }
            else
            {
                MessageBox.Show("Statue with code " + chargeValues["Code"] + " not found");
                return null;
            }
            charge.ChargeNumber = short.Parse(chargeValues["ChargeNumber"]);
            charge.Date = DateTime.Parse(chargeValues["Occurred"]);
            charge.Notes = chargeValues["OfficerNotes"];

            return charge;

        }


        private Dictionary<string, string> GetChildValues(XElement parent)
        {
            Dictionary<string, string> childValues = new Dictionary<string, string>();
            foreach (XElement descendent in parent.Descendants())
            {
                childValues.Add(descendent.Name.ToString(), descendent.Value);
            }
            return childValues;
        }
    }
}
