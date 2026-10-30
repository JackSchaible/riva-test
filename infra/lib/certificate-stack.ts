import * as cdk from "aws-cdk-lib";
import * as acm from "aws-cdk-lib/aws-certificatemanager";
import * as route53 from "aws-cdk-lib/aws-route53";
import { Construct } from "constructs";
import { DemoConfig } from "./config";

export interface CertificateStackProps extends cdk.StackProps {
  config: DemoConfig;
}

/** The frontend's certificate. CloudFront only accepts certificates from us-east-1, so it gets its own stack. */
export class CertificateStack extends cdk.Stack {
  readonly certificate: acm.ICertificate;

  constructor(scope: Construct, id: string, props: CertificateStackProps) {
    super(scope, id, { ...props, crossRegionReferences: true });

    const zone = route53.HostedZone.fromHostedZoneAttributes(this, "Zone", {
      hostedZoneId: props.config.hostedZoneId,
      zoneName: props.config.zoneName,
    });

    this.certificate = new acm.Certificate(this, "SiteCertificate", {
      domainName: props.config.siteDomain,
      validation: acm.CertificateValidation.fromDns(zone),
    });
  }
}
