//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.Storage.Auth;
using EdiEngine;
using EdiEngine.Runtime;
using System.Collections.Generic;

using SegmentDefinitions = EdiEngine.Standards.X12_004030.Segments;
using System.Linq;

namespace EdiSplitterFunc
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
        ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var BLOBDetails = JObject.Parse(requestBody);

            // Looking for something like this to be passed in the POST BODY:  {"ediFileName":"916386MS210_202001190125_000000001.raw","splitHalf":"1"}
            string BLOBFileName = BLOBDetails["ediFileName"].ToString();
            string splitHalf = BLOBDetails["splitHalf"].ToString();

            await SplitEDIFile(BLOBFileName, splitHalf, log);
            
            return (ActionResult)new OkObjectResult($"Request Body= {requestBody}");
        }

        public static async Task<String> SplitEDIFile(string BLOBFileName, string splitHalf, ILogger log) 
        {
            log.LogInformation("SplitEDIFile function processing a request.");

            // Parse the connection string and get a reference to the blob.
            var storageCredentials = new StorageCredentials("<your_storage_acct_Name>", "<your_storage_acct_Key>");
            var cloudStorageAccount = new CloudStorageAccount(storageCredentials, true);
            var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();

            //Define BlobIN Stream
            CloudBlobClient blobINClient = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobINContainer = blobINClient.GetContainerReference("edi-process-split");
            CloudBlockBlob blobIN = blobINContainer.GetBlockBlobReference(BLOBFileName);
            Stream blobINStream = await blobIN.OpenReadAsync();

            //Define BlobOUT FileName - for FIRST OR SECOND half of transactions
            string blobOUTPreFix = "";
            if (splitHalf == "1"){
                blobOUTPreFix = "A_";
            }
            else {
                blobOUTPreFix = "B_";
            }
            CloudBlobClient blobOUTClient = cloudStorageAccount.CreateCloudBlobClient();
            CloudBlobContainer blobOUTContainer = blobOUTClient.GetContainerReference("edi-process-split");
            CloudBlockBlob blobOUT = blobOUTContainer.GetBlockBlobReference(blobOUTPreFix + BLOBFileName);

            //Read blobIN             
            EdiDataReader r = new EdiDataReader();
            EdiBatch b = r.FromStream(blobINStream);

            int totEDITransCtr = 0;
            totEDITransCtr = b.Interchanges[0].Groups[0].Transactions.Count();

            // **********************************************************
            // ** SAVE Original EDI Interchange: Data & Transactions
            // **********************************************************
            EdiInterchange OrigInter = new EdiInterchange();
            OrigInter = b.Interchanges[0];

            List<EdiTrans> OrigTrans = new List<EdiTrans>();
            OrigTrans = b.Interchanges[0].Groups[0].Transactions;

            // **********************************************************
            // ** Calculate SPLIT
            // **********************************************************

            //1st Half
            int EDI_Trans_Ctr1 = totEDITransCtr / 2;

            //2nd Half
            int EDI_Trans_Ctr2 = totEDITransCtr - (totEDITransCtr / 2);

            // **********************************************************
            // ** Write-Out 1st Half to file
            // **********************************************************
            List<EdiTrans> HalfTrans = new List<EdiTrans>();

            //Define LOOP Parameters - for FIRST OR SECOND half of transactions
            int loopStart = 0;
            int loopEnd = 0;

            if (splitHalf == "1")
            {
                loopStart = 0;
                loopEnd = EDI_Trans_Ctr1;
            }
            else
            {
                loopStart = EDI_Trans_Ctr1 + 1;
                loopEnd = totEDITransCtr;
            }
            //Process TRANSACTIONS
            for (int i = loopStart; i < loopEnd; i++)
            {
                EdiTrans ediItem = new EdiTrans();
                ediItem = b.Interchanges[0].Groups[0].Transactions[i];
                HalfTrans.Add(ediItem);
            }
            EdiBatch b2 = new EdiBatch();

            //Handle InterChange
            EdiInterchange ediInterChgItem = new EdiInterchange();
            ediInterChgItem = b.Interchanges[0];

            //Remove existing
            EdiGroup jbEdiGroupItem = new EdiGroup("");
            jbEdiGroupItem = b.Interchanges[0].Groups[0];
            ediInterChgItem.Groups.RemoveRange(0, 1);

            //Add Interchange
            b2.Interchanges.Add(ediInterChgItem);

            //Handle Group
            jbEdiGroupItem.Transactions.RemoveRange(0, totEDITransCtr);
            ediInterChgItem.Groups.Add(jbEdiGroupItem);

            //Add Transactions
            for (int i = 0; i < HalfTrans.Count(); i++) //Hardcoded to 91
            {
                EdiTrans ediItem = new EdiTrans();
                ediItem = HalfTrans[i];
                b2.Interchanges[0].Groups[0].Transactions.Add(ediItem);
            }
            EdiDataWriterSettings settings = new EdiDataWriterSettings(
                new SegmentDefinitions.ISA(),
                new SegmentDefinitions.IEA(),
                new SegmentDefinitions.GS(),
                new SegmentDefinitions.GE(),
                new SegmentDefinitions.ST(),
                new SegmentDefinitions.SE(),
                OrigInter.ISA.Content[4].ToString(), //isaSenderQual
                OrigInter.ISA.Content[5].ToString(), //isaSenderId
                OrigInter.ISA.Content[6].ToString(), //isaReceiverQual
                                                     // OrigInter.ISA.Content[7].ToString(), //isaReceiverId
                "9163863M210", //isaReceiverId
                OrigInter.ISA.Content[8].ToString(), //gsSenderId
                OrigInter.ISA.Content[9].ToString(), //gsReceiverId

                // OrigInter.ISA.Content[10].ToString(), //JB Test

                OrigInter.ISA.Content[11].ToString(), //isaEdiVersion
                OrigInter.ISA.Content[12].ToString(), //gsEdiVersion
                                                      //  "00403", //gsEdiVersion
                OrigInter.ISA.Content[14].ToString(), //P //isaUsageIndicator //[
                000, //isaFirstControlNumber
                001, //gsFirstControlNumber
                OrigInter.SegmentSeparator.ToString(), //segmentSeparator
                OrigInter.ElementSeparator.ToString());//elementSeparator

            EdiDataWriter w = new EdiDataWriter(settings);
            string JBData = w.WriteToString(b2);
            log.LogInformation("trigger function - WRITING SPLIT FILE : " + blobOUT.Name.ToString());
            await blobOUT.UploadTextAsync(JBData);
            blobINStream.Close();
            return " ";
        }

    }
}
